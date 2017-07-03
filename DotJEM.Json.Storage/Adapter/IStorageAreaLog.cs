using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using DotJEM.Json.Storage.Adapter.Materialize;
using DotJEM.Json.Storage.Adapter.Materialize.ChanceLog;
using DotJEM.Json.Storage.Adapter.Materialize.Log;
using DotJEM.Json.Storage.Configuration;
using DotJEM.Json.Storage.Queries;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Adapter
{
    public interface IStorageAreaLog
    {
        long Generation { get; }
        IStorageChangeCollection Get(bool includeDeletes = true, int count = 5000);
        IStorageChangeCollection Get(long token, bool includeDeletes = true, int count = 5000);
    }

    public class SqlServerStorageAreaLog : IStorageAreaLog
    {
        private bool initialized;
        private long previousToken = -1;
        private readonly SqlServerStorageArea area;
        private readonly SqlServerStorageContext context;
        private readonly object padlock = new object();

        public long Generation => previousToken;

        public SqlServerStorageAreaLog(SqlServerStorageArea area, SqlServerStorageContext context)
        {
            this.area = area;
            this.context = context;

            this.indexes = new Lazy<Dictionary<string, IndexDefinition>>(() => LoadIndexes().ToDictionary(def => def.Name));
        }

        public IStorageChangeCollection Insert(Guid id, JObject original, JObject changed, ChangeType action, SqlConnection connection, SqlTransaction transaction)
        {
            EnsureTable();

            using (SqlCommand command = new SqlCommand { Connection = connection, Transaction = transaction })
            {
                command.CommandText = area.Commands["InsertChange"];
                command.Parameters.Add(new SqlParameter(StorageField.Fid.ToString(), SqlDbType.UniqueIdentifier)).Value = id;
                command.Parameters.Add(new SqlParameter(LogField.Action.ToString(), SqlDbType.VarChar)).Value = action.ToString();
                command.Parameters.Add(new SqlParameter(StorageField.Data.ToString(), SqlDbType.VarBinary)).Value = context.Serializer.Serialize(Diff(original, changed));

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    reader.Read();
                    long token = reader.GetInt64(reader.GetOrdinal(StorageField.Id.ToString()));
                    reader.Close(); ;
                    
                    return new StorageChangeCollection(area.Name, token, new List<Change> {
                        //TODO: New change type which is already materialized to a JSON object.
                        new SqlServerInsertedChange(changed, token, action, id)
                    });
                }

            }
        }

        private StorageChangeCollection RunDataReader(long startToken, SqlDataReader reader)
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

            List<Change> changes = new List<Change>();
            while (reader.Read())
            {
                long token = reader.GetInt64(tokenColumn);

                ChangeType changeType;
                Enum.TryParse(reader.GetString(actionColumn), out changeType);

                try
                {
                    Change change = changeType != ChangeType.Delete
                        ? (Change)new SqlServerEntityChange(
                            MaterializeChange,
                            token,
                            changeType,
                            reader.GetGuid(idColumn),
                            reader.GetString(contentTypeColumn),
                            area.Name,
                            reader.GetInt64(refColumn),
                            reader.GetInt32(versionColumn),
                            reader.GetDateTime(createdColumn),
                            reader.GetDateTime(updatedColumn),
                            reader.GetSqlBinary(dataColumn).Value)
                        : new SqlServerDeleteChange(MaterializeChange, token, changeType, reader.GetGuid(fidColumn));
                    changes.Add(change);
                }
                catch (Exception exception)
                {
                    changes.Add(new FaultyChange(token, changeType, reader.GetGuid(fidColumn), exception));
                }
            }
            if (changes.Any())
            {
                return new StorageChangeCollection(area.Name, changes.Last().Token, changes);
            }
            return new StorageChangeCollection(area.Name, startToken, changes);
        }

        private JObject MaterializeChange(SqlServerEntityChange arg)
        {
            JObject json = context.Serializer.Deserialize(arg.Data);
            json[context.Configuration.Fields[JsonField.Id]] = arg.Id;
            json[context.Configuration.Fields[JsonField.Reference]] = Base36.Encode(arg.Reference);
            json[context.Configuration.Fields[JsonField.Area]] = arg.Area;
            json[context.Configuration.Fields[JsonField.Version]] = arg.Version;
            json[context.Configuration.Fields[JsonField.ContentType]] = arg.ContentType;
            json[context.Configuration.Fields[JsonField.Created]] = DateTime.SpecifyKind(arg.Created, DateTimeKind.Utc);
            json[context.Configuration.Fields[JsonField.Updated]] = DateTime.SpecifyKind(arg.Updated, DateTimeKind.Utc);
            json = area.Migrate(json);
            return json;
        }

        private JObject MaterializeChange(SqlServerDeleteChange arg)
        {
            JObject json = new JObject();
            json[context.Configuration.Fields[JsonField.Id]] = arg.Id;
            json[context.Configuration.Fields[JsonField.ContentType]] = "Dummy";
            return json;
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
            return Get(previousToken, includeDeletes, count);
        }

        public IStorageChangeCollection Get(long token, bool includeDeletes = true, int count = 5000)
        {
            if (!TableExists)
                return new StorageChangeCollection(area.Name, -1, new List<Change>());

            using (SqlConnection connection = context.Connection())
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand { Connection = connection })
                {
                    command.CommandTimeout = context.Configuration.ReadCommandTimeout;
                    command.CommandText = includeDeletes
                        ? area.Commands["SelectChangesWithDeletes"]
                        : area.Commands["SelectChangesNoDeletes"];
                    command.Parameters.Add(new SqlParameter("token", SqlDbType.BigInt)).Value = token;
                    command.Parameters.Add(new SqlParameter("count", SqlDbType.Int)).Value = count;

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        StorageChangeCollection changes = RunDataReader(token, reader);
                        previousToken = changes.Token;
                        return changes;
                    }
                }
            }
        }

        private void EnsureTable()
        {
            if (initialized)
                return;

            if (!TableExists)
                CreateTable();

            EnsureIndexes();

            initialized = true;
        }

        private void EnsureIndexes()
        {
            EnsureIndex($"{area.Name}.changelog.id_fid_index", "ChangeLogIdFidIndex");
            EnsureIndex($"{area.Name}.changelog.id_fid_action_index", "ChangeLogIdFidActionIndex");
            EnsureIndex($"{area.Name}.changelog.fid_id_index", "ChangeLogFidIdIndex");
        }

        private void EnsureIndex(string name, string commandName)
        {
            if (Indexes.ContainsKey(name))
                return;

            lock (padlock)
            {
                if (Indexes.ContainsKey(name))
                    return;

                CreateIndex(commandName);
            }
        }

        private void CreateIndex(string commandName)
        {
            using (SqlConnection connection = context.Connection())
            {

                connection.Open();
                using (SqlCommand command = new SqlCommand { Connection = connection })
                {
                    command.CommandText = area.Commands[commandName];
                    command.ExecuteNonQuery();
                }
            }
        }

        private Dictionary<string, IndexDefinition> Indexes => indexes.Value;

        //TODO: Lazy evaliated result set: The lazy definition + load method could probably be mixed into a single construct making this easier in the future to manage other such constructs.
        private readonly Lazy<Dictionary<string, IndexDefinition>> indexes;

        private IEnumerable<IndexDefinition> LoadIndexes()
        {
            using (SqlConnection connection = context.Connection())
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand { Connection = connection })
                {
                    command.CommandText = area.Commands["ChangeLogIndexes"];
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        int nameColumn = reader.GetOrdinal("name");
                        while (reader.Read())
                        {
                            string name = reader.GetString(nameColumn);

                            yield return new IndexDefinition(name);
                        }
                    }
                }
            }
        }
        //TODO: Lazy evaliated result set end: 

        private bool TableExists
        {
            get
            {
                using (SqlConnection connection = context.Connection())
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand { Connection = connection })
                    {
                        command.CommandText = area.Commands["LogTableExists"];
                        object result = command.ExecuteScalar();
                        return 1 == Convert.ToInt32(result);
                    }
                }
            }
        }

        private void CreateTable()
        {
            using (SqlConnection connection = context.Connection())
            {
                lock (padlock)
                {
                    if (TableExists)
                        return;

                    connection.Open();
                    using (SqlCommand command = new SqlCommand { Connection = connection })
                    {
                        command.CommandText = area.Commands["CreateLogTable"];
                        command.ExecuteNonQuery();
                    }
                }
            }
        }



        private class IndexDefinition
        {
            public string Name { get; }

            public IndexDefinition(string name)
            {
                Name = name;
            }

        }
    }

}