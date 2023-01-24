using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Security.Cryptography.X509Certificates;
using DotJEM.Json.Storage.Adapter.Materialize;
using DotJEM.Json.Storage.Adapter.Materialize.ChanceLog;
using DotJEM.Json.Storage.Adapter.Materialize.ChanceLog.ChangeObjects;
using DotJEM.Json.Storage.Adapter.Materialize.Log;
using DotJEM.Json.Storage.Adapter.Observable;
using DotJEM.Json.Storage.Configuration;
using DotJEM.Json.Storage.Queries;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Adapter;

public interface IStorageAreaLog
{
    /// <summary>
    /// Gets the latest generation returned by this changelog.
    /// </summary>
    long CurrentGeneration { get; }

    /// <summary>
    /// Gets the latest generation stored in the database.
    /// </summary>
    long LatestGeneration { get; }

    /// <summary>
    /// Gets the next batch of changes.
    /// </summary>
    /// <remarks>
    /// Use this method to continiously pool for changed documents in the storage area while letting the <see cref="IStorageAreaLog"/> track which generation was returned last.
    /// </remarks>
    /// <param name="includeDeletes">If <code>true</code>, returns all types of changes; If <code>false</code>, it skips deletes.</param>
    /// <param name="count">The maximum number of changes to return.</param>
    /// <returns>A <see cref="IStorageChangeCollection"/> with changes since <see cref="CurrentGeneration"/></returns>
    IStorageChangeCollection Get(bool includeDeletes = true, int count = 5000);

    /// <summary>
    /// Gets a batch of changes from the provided <see cref="generation"/>.
    /// </summary>
    /// <remarks>
    /// Use this method to continiously pool for changed documents in the storage area while taking over tracking of the last returned generation.
    /// <strong>Note:</strong>If <see cref="count"/> is less than <code>1</code>, then this method will just reset <see cref="CurrentGeneration"/> to
    /// the <see cref="generation"/> provided unless the <see cref="generation"/> provided is greater than <see cref="LatestGeneration"/>, in which case
    /// <see cref="CurrentGeneration"/> is set to <see cref="LatestGeneration"/>.
    /// </remarks>
    /// <param name="generation">The generation to start from.</param>
    /// <param name="includeDeletes">If <code>true</code>, returns all types of changes; If <code>false</code>, it skips deletes.</param>
    /// <param name="count">The maximum number of changes to return.</param>
    /// <returns></returns>
    IStorageChangeCollection Get(long generation, bool includeDeletes = true, int count = 5000);

    /// <summary>
    /// Experimental!
    /// </summary>
    /// <param name="generation"></param>
    /// <param name="includeDeletes"></param>
    /// <returns></returns>
    IStorageAreaLogReader OpenLogReader(long generation, bool includeDeletes = true);

    /// <summary>
    /// Experimental!
    /// Opens a IStorageAreaLogObserveable which on subscribtion will start fetching changes from the provided intialGeneration and
    /// then keep raising events when changes are detected until disposed.
    /// </summary>
    /// <param name="initialGeneration"></param>
    /// <param name="includeDeletes"></param>
    /// <returns></returns>
    IStorageAreaLogObservable OpenObservable(long initialGeneration = 0, bool includeDeletes = true);
}

public partial class SqlServerStorageAreaLog : IStorageAreaLog
{
    private bool initialized;
    private readonly SqlServerStorageArea area;
    private readonly SqlServerStorageContext context;
    private readonly object padlock = new();

    public long CurrentGeneration { get; private set; } = -1;

    public long LatestGeneration
    {
        get
        {
            if (!TableExists)
                return -1;

            using SqlConnection connection = context.Connection();
            connection.Open();
            using SqlCommand command = new();
            command.Connection = connection;
            command.CommandTimeout = context.SqlServerConfiguration.ReadCommandTimeout;
            command.CommandText = area.Commands["SelectMaxGeneration"];
            object maxGeneration = command.ExecuteScalar();
            if (maxGeneration == null || maxGeneration is DBNull)
                return -1;
            return (long)maxGeneration;
        }
    }

    public SqlServerStorageAreaLog(SqlServerStorageArea area, SqlServerStorageContext context)
    {
        this.area = area;
        this.context = context;

        this.indexes = new Lazy<Dictionary<string, IndexDefinition>>(() => LoadIndexes().ToDictionary(def => def.Name));
    }

    public IStorageChangeCollection Insert(Guid id, JObject original, JObject changed, ChangeType action, SqlConnection connection, SqlTransaction transaction)
    {
        EnsureTable();

        using SqlCommand command = new() { Connection = connection, Transaction = transaction };
        command.CommandText = area.Commands["InsertChange"];
        command.Parameters.Add(new SqlParameter(StorageField.Fid.ToString(), SqlDbType.UniqueIdentifier)).Value = id;
        command.Parameters.Add(new SqlParameter(LogField.Action.ToString(), SqlDbType.VarChar)).Value = action.ToString();
        command.Parameters.Add(new SqlParameter(StorageField.Data.ToString(), SqlDbType.VarBinary)).Value = context.Serializer.Serialize(Diff(original, changed));

        using SqlDataReader reader = command.ExecuteReader();
        reader.Read();
        long token = reader.GetInt64(reader.GetOrdinal(StorageField.Id.ToString()));
        reader.Close();

        string reference = (string)(changed ?? original)[context.Configuration.Fields[JsonField.Reference]];
        string contentType = (string)(changed ?? original)[context.Configuration.Fields[JsonField.ContentType]];
        int version = (int)(changed ?? original)[context.Configuration.Fields[JsonField.Version]];
        DateTime created = (DateTime)(changed ?? original)[context.Configuration.Fields[JsonField.Created]];
        DateTime updated = (DateTime)(changed ?? original)[context.Configuration.Fields[JsonField.Updated]];

        ChangeLogRow row = action switch
        {
            ChangeType.Create => new CreateOnChangeLogRow(context, area.Name, token, id, contentType, Base36.Decode(reference), version, created, updated, changed),
            ChangeType.Update => new UpdateOnChangeLogRow(context, area.Name, token, id, contentType, Base36.Decode(reference), version, created, updated, changed),
            ChangeType.Delete => new DeleteOnChangeLogRow(context, area.Name, token, id, contentType, Base36.Decode(reference), version, created, updated, original),
            _ => throw new ArgumentOutOfRangeException()
        };
        return new StorageChangeCollection(area.Name, token, new List<IChangeLogRow> { row });
    }

    private JObject Diff(JObject original, JObject changed)
    {
        JObject either = original ?? changed;
        JObject change = new JObject();
        change[context.Configuration.Fields[JsonField.ContentType]] = either[context.Configuration.Fields[JsonField.ContentType]];
        //TODO: Implemnt simple diff (record changed properties)
        //      - Could also use this for change details...
        return change;
    }

    public IStorageChangeCollection Get(bool includeDeletes = true, int count = 5000)
    {
        if (!TableExists)
            return new StorageChangeCollection(area.Name, -1, new List<IChangeLogRow>());

        return GetFromGeneration(CurrentGeneration, includeDeletes, count);
    }

    public IStorageChangeCollection Get(long generation, bool includeDeletes = true, int count = 5000)
    {
        //Note: If the requested token is greater than the current generation, we fetch the latest generation.
        //      This ensures that the generation actually exists so that we don't skip future generation.
        if (generation > CurrentGeneration) generation = Math.Min(generation, LatestGeneration);

        return GetFromGeneration(generation, includeDeletes, count);
    }

    public IStorageAreaLogReader OpenLogReader(long generation, bool includeDeletes = true)
    {
        if (!TableExists)
            return new NullStorageAreaLogReader();

        SqlConnection connection = context.Connection();
        connection.Open();
        SqlCommand command = new();
        command.Connection = connection;
        command.CommandTimeout = context.SqlServerConfiguration.ReadCommandTimeout;
        command.CommandText = includeDeletes
            ? area.Commands["SelectChangesObserver"]
            : area.Commands["SelectChangesObserverInit"];
        command.Parameters.Add(new SqlParameter("token", SqlDbType.BigInt)).Value = generation;
        return new StorageAreaLogReader(context, area.Name, connection, command, gen => this.CurrentGeneration = gen);
    }


    private IStorageChangeCollection GetFromGeneration(long generation, bool includeDeletes, int count)
    {
        if (!TableExists)
            return new StorageChangeCollection(area.Name, -1, new List<IChangeLogRow>());

        if (count < 1)
        {
            //Note: If count is 0 or less, we don't load any changes, but only resets the generation.
            CurrentGeneration = generation;
            return new StorageChangeCollection(area.Name, CurrentGeneration, new List<IChangeLogRow>());
        }

        using SqlConnection connection = context.Connection();
        connection.Open();
        using SqlCommand command = new();
        command.Connection = connection;
        command.CommandTimeout = context.SqlServerConfiguration.ReadCommandTimeout;
        command.CommandText = includeDeletes
            ? area.Commands["SelectChangesWithDeletes"]
            : area.Commands["SelectChangesNoDeletes"];
        command.Parameters.Add(new SqlParameter("token", SqlDbType.BigInt)).Value = generation;
        command.Parameters.Add(new SqlParameter("count", SqlDbType.Int)).Value = count;

        using SqlDataReader reader = command.ExecuteReader();
        StorageChangeCollection changes = RunDataReader(generation, reader);
        CurrentGeneration = changes.Generation;
        return changes;
    }

    private StorageChangeCollection RunDataReader(long startGeneration, SqlDataReader reader)
    {
        List<IChangeLogRow> changes = EnumerateReader(reader).ToList();
        if (changes.Any())
        {
            return new StorageChangeCollection(area.Name, changes.Last().Generation, changes);
        }
        return new StorageChangeCollection(area.Name, startGeneration, changes);
    }

    private IEnumerable<IChangeLogRow> EnumerateReader(SqlDataReader reader)
    {
        int tokenColumn = reader.GetOrdinal("Token");
        int actionColumn = reader.GetOrdinal("Action");
        int idColumn = reader.GetOrdinal(StorageField.Id.ToString());
        int fidColumn = reader.GetOrdinal(StorageField.Fid.ToString());
        int dataColumn = reader.GetOrdinal(StorageField.Data.ToString());

        int refColumn = reader.GetOrdinal(StorageField.Reference.ToString());
        int versionColumn = reader.GetOrdinal(StorageField.Version.ToString());
        int contentTypeColumn = reader.GetOrdinal(StorageField.ContentType.ToString());
        int createdColumn = reader.GetOrdinal(StorageField.Created.ToString());
        int updatedColumn = reader.GetOrdinal(StorageField.Updated.ToString());

        while (reader.Read())
        {
            long token = reader.GetInt64(tokenColumn);
            Enum.TryParse(reader.GetString(actionColumn), out ChangeType changeType);
            yield return CreateRow();
            IChangeLogRow CreateRow()
            {
                try
                {
                    ChangeLogRow row = changeType switch {
                        ChangeType.Create => new CreateChangeLogRow(context, area.Name, token, reader.GetGuid(idColumn), reader.GetString(contentTypeColumn), reader.GetInt64(refColumn), reader.GetInt32(versionColumn), reader.GetDateTime(createdColumn), reader.GetDateTime(updatedColumn), reader.GetSqlBinary(dataColumn).Value),
                        ChangeType.Update => new UpdateChangeLogRow(context, area.Name, token, reader.GetGuid(idColumn), reader.GetString(contentTypeColumn), reader.GetInt64(refColumn), reader.GetInt32(versionColumn), reader.GetDateTime(createdColumn), reader.GetDateTime(updatedColumn), reader.GetSqlBinary(dataColumn).Value),
                        ChangeType.Delete => new DeleteChangeLogRow(context, area.Name, token, reader.GetGuid(fidColumn)),
                        _ => throw new ArgumentOutOfRangeException()
                    };
                    return row;
                }
                catch (Exception exception)
                {
                    return new FaultyChangeLogRow(context, area.Name, token, reader.GetGuid(fidColumn), changeType, exception);
                }
            }
        }
    }


    public IStorageAreaLogObservable OpenObservable(long initialGeneration = 0, bool includeDeletes = true)
    {
        return includeDeletes
            ? new DefaultSqlServerStorageAreaLogObservable(this, area, context, initialGeneration)
            : throw new NotSupportedException("Not implemented at this point.");
        //new SkipDeletesSqlServerStorageAreaLogObserveable(this, area, context, initialGeneration);
    }
}

internal class NullStorageAreaLogReader : IStorageAreaLogReader
{
    public IEnumerator<IChangeLogRow> GetEnumerator()
    {
        yield break;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose() {}
}

internal class StorageAreaLogReader : IStorageAreaLogReader
{
    private readonly SqlConnection connection;
    private readonly SqlCommand command;
    private readonly SqlDataReader reader;

    private readonly Action<long> updateGen;
    private readonly ChangeLogRowFactory rowFactory;

    internal StorageAreaLogReader(IStorageContext context, string area, SqlConnection connection, SqlCommand command, Action<long> updateGen)
    {
        this.connection = connection;
        this.command = command;
        this.updateGen = updateGen;
        this.reader = command.ExecuteReader();
        this.rowFactory = new ChangeLogRowFactory(context, area, SqlServerChangeLogColumnSet.FromReader(reader));
    }

    public IEnumerator<IChangeLogRow> GetEnumerator()
    {
        return EnumerateReader()
            .Select(ch => {
                updateGen(ch.Generation);
                return ch;
            })
            .GetEnumerator();
    }

    private IEnumerable<IChangeLogRow> EnumerateReader()
    {
        while (reader.Read())
            yield return rowFactory.CreateFrom(reader);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose()
    {
        reader?.Dispose();
        connection?.Dispose();
        command?.Dispose();
    }
}