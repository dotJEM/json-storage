using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using DotJEM.Json.Storage.Adapter.Materialize.ChanceLog.ChangeObjects;
using DotJEM.Json.Storage.Queries;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Adapter.Observable;

public interface IStorageAreaLogObservable : IObservable<IChangeLogRow>
{

}

public abstract class AbstractSqlServerStorageAreaLogObservable : IStorageAreaLogObservable
{
    private readonly ConcurrentDictionary<Guid, IChangeObserverState> observers = new();
  
    public IDisposable Subscribe(IObserver<IChangeLogRow> observer)
    {
        return observers
            .GetOrAdd(Guid.NewGuid(), id =>CreateObserverSubscription(id, observer))
            .Start(CancellationToken.None);
    }

    protected bool DetachObserver(Guid id) => observers.TryRemove(id, out _);

    protected abstract IChangeObserverState CreateObserverSubscription(Guid id, IObserver<IChangeLogRow> observer);
}

public class DefaultSqlServerStorageAreaLogObservable : AbstractSqlServerStorageAreaLogObservable
{
    private readonly SqlServerStorageAreaLog log;
    private readonly SqlServerStorageContext context;
    private readonly ICommandFactory commands;
    private readonly long generation;

    public DefaultSqlServerStorageAreaLogObservable(
        SqlServerStorageAreaLog log, 
        SqlServerStorageArea area,
        SqlServerStorageContext context,
        long generation)
    {
        this.log = log;
        this.context = context;
        this.commands = area.Commands;
        this.generation = generation;
    }


    protected override IChangeObserverState CreateObserverSubscription(Guid id, IObserver<IChangeLogRow> observer)
    {
        ChangeObserverState state = new ChangeObserverState(
            context,
            (string)commands["SelectChangesObserver"], observer, 
            () => DetachObserver(id));
        return state;
    }


    private class ChangeObserverState : IChangeObserverState
    {
        private readonly SqlServerStorageContext context;
        private readonly string command;
        private readonly Action detach;
        private long generation = 0;
            
        private readonly IObserver<IChangeLogRow> observer;

        public ChangeObserverState(SqlServerStorageContext context, string command, IObserver<IChangeLogRow> observer, Action detach)
        {
            this.command = command;
            this.observer = observer;
            this.detach = detach;
            this.context = context;
        }

        public IChangeObserverState Start(CancellationToken cancellation)
        {
            Task.Run(async  () =>
            {
                while (!cancellation.IsCancellationRequested)
                {
                    using SqlConnection connection = context.Connection();
                    await connection.OpenAsync(cancellation);

                    using SqlCommand command = new();
                    command.Connection = connection;
                    command.CommandTimeout = context.SqlServerConfiguration.ReadCommandTimeout;
                    command.CommandText = this.command;
                    command.Parameters.Add(new SqlParameter("token", SqlDbType.BigInt)).Value = generation;

                    using ChangeLogSqlDataReader reader = new ChangeLogSqlDataReader(context, "", await command.ExecuteReaderAsync(cancellation));
                    foreach (IChangeLogRow changeLogRow in reader)
                    {
                        observer.OnNext(changeLogRow);
                        generation = changeLogRow.Generation;
                    }
                    break;
                    //TODO: (jmd 2023-01-16) Run forever until signaled for dispose.
                    //await Task.Delay(10000, cancellation);
                }
                observer.OnCompleted();
            }, cancellation);
            return this;
        }
        
        public void Dispose()
        {
            detach();
        }
    }

}

//public class SkipDeletesSqlServerStorageAreaLogObserveable : AbstractSqlServerStorageAreaLogObserveable
//{

//    private readonly SqlServerStorageAreaLog log;
//    private readonly SqlServerStorageContext context;
//    private readonly ICommandFactory commands;
//    private readonly long generation;

//    public SkipDeletesSqlServerStorageAreaLogObserveable(
//        SqlServerStorageAreaLog log, 
//        SqlServerStorageArea area,
//        SqlServerStorageContext context, 
//        long generation)
//    {
            
//        this.log = log;
//        this.context = context;
//        this.commands = area.Commands;
//        this.generation = generation;
//    }


//    protected override IChangeObserverState CreateObserverSubscribtion(Guid id, IObserver<IChangeLogRow> observer)
//    {
//        ChangeObserverState state = new ChangeObserverState(
//                (string)commands["SelectChangesObserver"], observer, 
//                () => DetachObserver(id));
//        return state;
//    }


//    private class ChangeObserverState : IChangeObserverState
//    {
//        private readonly SqlServerStorageContext context;
//        private readonly string command;
//        private readonly Action detach;
//        private readonly long generation = 0;
            
//        private readonly IObserver<IChangeLogRow> observer;

//        public ChangeObserverState(string command, IObserver<IChangeLogRow> observer, Action detach)
//        {
//            this.command = command;
//            this.observer = observer;
//            this.detach = detach;
//        }

//        public IChangeObserverState Start(CancellationToken cancellation)
//        {
//            using SqlConnection connection = context.Connection();
//            connection.Open();

//            using SqlCommand command = new();
//            command.Connection = connection;
//            command.CommandTimeout = context.SqlServerConfiguration.ReadCommandTimeout;
//            command.CommandText = this.command;
//            command.Parameters.Add(new SqlParameter("token", SqlDbType.BigInt)).Value = generation;

//            using SqlDataReader reader = command.ExecuteReader();
//            StorageChangeCollection changes = RunDataReader(generation, reader);
//            CurrentGeneration = changes.Generation;
//            return new StorageAreaLogObserveable();
//        }

//        public void Dispose() => detach();
//    }
//}
public interface IChangeObserverState: IDisposable
{
    IChangeObserverState Start(CancellationToken cancellation);
}


public class ChangeLogRowFactory
{
    private readonly IStorageContext context;
    private readonly string area;
    private readonly SqlServerChangeLogColumnSet columnSet;

    internal ChangeLogRowFactory(IStorageContext context, string area, SqlServerChangeLogColumnSet columnSet)
    {
        this.context = context;
        this.area = area;
        this.columnSet = columnSet;
    }

    public IChangeLogRow CreateFrom(SqlDataReader reader)
    {
        long token = reader.GetInt64(columnSet.TokenColumn);
        Enum.TryParse(reader.GetString(columnSet.ActionColumn), out ChangeType changeType);
        try
        {
            switch (changeType)
            {
                case ChangeType.Create:
                    using (InjectingJsonReader jsonReader = new(context.Serializer.OpenReader(reader.GetSqlBinary(columnSet.DataColumn).Value)))
                    {
                        return new CreateOnChangeLogRow(context,
                            area,
                            token,
                            reader.GetGuid(columnSet.IDColumn),
                            reader.GetString(columnSet.ContentTypeColumn),
                            reader.GetInt64(columnSet.RefColumn),
                            reader.GetInt32(columnSet.VersionColumn),
                            reader.GetDateTime(columnSet.CreatedColumn),
                            reader.GetDateTime(columnSet.UpdatedColumn),
                            JObject.Load(jsonReader));
                    }
                    break;
                case ChangeType.Update:
                    using (InjectingJsonReader jsonReader = new(context.Serializer.OpenReader(reader.GetSqlBinary(columnSet.DataColumn).Value)))
                    {
                        return new UpdateOnChangeLogRow(context,
                            area,
                            token,
                            reader.GetGuid(columnSet.IDColumn),
                            reader.GetString(columnSet.ContentTypeColumn),
                            reader.GetInt64(columnSet.RefColumn),
                            reader.GetInt32(columnSet.VersionColumn),
                            reader.GetDateTime(columnSet.CreatedColumn),
                            reader.GetDateTime(columnSet.UpdatedColumn),
                            JObject.Load(jsonReader));
                    }

                    break;
                case ChangeType.Delete:
                    return new DeleteChangeLogRow(
                        context,
                        area,
                        token,
                        reader.GetGuid(columnSet.FidColumn)
                    );
                    break;
                case ChangeType.Faulty:
                default:
                    throw new ArgumentOutOfRangeException();
            }

        }
        catch (Exception exception)
        {
            return new FaultyChangeLogRow(context, area, token, reader.GetGuid(columnSet.FidColumn), changeType, exception);
        }
    }
}

internal class SqlServerChangeLogColumnSet
{
    public static SqlServerChangeLogColumnSet FromReader(SqlDataReader reader)
    {
        return new SqlServerChangeLogColumnSet {

            TokenColumn = reader.GetOrdinal("Token"),
            ActionColumn = reader.GetOrdinal("Action"),
            IDColumn = reader.GetOrdinal(StorageField.Id.ToString()),
            FidColumn = reader.GetOrdinal(StorageField.Fid.ToString()),
            DataColumn = reader.GetOrdinal(StorageField.Data.ToString()),

            RefColumn = reader.GetOrdinal(StorageField.Reference.ToString()),
            VersionColumn = reader.GetOrdinal(StorageField.Version.ToString()),
            ContentTypeColumn = reader.GetOrdinal(StorageField.ContentType.ToString()),
            CreatedColumn = reader.GetOrdinal(StorageField.Created.ToString()),
            UpdatedColumn = reader.GetOrdinal(StorageField.Updated.ToString()),
        };
    }

    public int UpdatedColumn { get; set; }

    public int CreatedColumn { get; set; }

    public int ContentTypeColumn { get; set; }

    public int VersionColumn { get; set; }

    public int RefColumn { get; set; }

    public int DataColumn { get; set; }

    public int FidColumn { get; set; }

    public int IDColumn { get; set; }

    public int ActionColumn { get; set; }

    public int TokenColumn { get; set; }
}

public interface IStorageAreaLogReader : IEnumerable<IChangeLogRow>, IDisposable
{

}

public class ChangeLogSqlDataReader : IStorageAreaLogReader
{
    private readonly SqlDataReader reader;
    private readonly ChangeLogRowFactory rowFactory;

    public ChangeLogSqlDataReader(IStorageContext context, string area, SqlDataReader reader)
    {
        this.reader = reader;
        this.rowFactory = new ChangeLogRowFactory(context, area, SqlServerChangeLogColumnSet.FromReader(reader));
    }
    public IEnumerator<IChangeLogRow> GetEnumerator()
    {
        while (reader.Read())
            yield return rowFactory.CreateFrom(reader);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose() => reader?.Dispose();
}