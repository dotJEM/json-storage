using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using DotJEM.Json.Storage.Configuration;
using DotJEM.Json.Storage.Queries;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Adapter
{
    public interface IStorageAreaLog
    {
        IStorageChanges Get(bool includeDeletes = true, int count = 5000);
        IStorageChanges Get(long token, bool includeDeletes = true, int count = 5000);
    }

    public interface IStorageChanges : IEnumerable<IStorageChange>
    {
        long Token { get; }
        ChangeCount Count { get; }
        IEnumerable<JObject> Created { get; }
        IEnumerable<JObject> Updated { get; }
        IEnumerable<JObject> Deleted { get; }
    }

    public struct ChangeCount
    {
        public int Total { get { return Created + Updated + Deleted; } }

        public int Created { get; private set; }
        public int Updated { get; private set; }
        public int Deleted { get; private set; }

        public ChangeCount(int created, int updated, int deleted)
            : this()
        {
            Created = created;
            Updated = updated;
            Deleted = deleted;
        }

        public static ChangeCount operator +(ChangeCount left, ChangeCount right)
        {
            return
                new ChangeCount()
                {
                    Created = left.Created + right.Created,
                    Updated = left.Updated + right.Updated,
                    Deleted = left.Deleted + right.Deleted
                };
        }

        public static implicit operator int(ChangeCount count)
        {
            return count.Total;
        }

        public override string ToString()
        {
            return $"Created: {Created}, Updated: {Updated}, Deleted: {Deleted}";
        }
    }

    public class StorageChanges : IStorageChanges
    {
        private readonly List<IStorageChange> changes;
        private readonly ILookup<ChangeType, JObject> changeLookup;
        private readonly Lazy<ChangeCount> count;

        public long Token { get; }
        public ChangeCount Count => count.Value;
        public IEnumerable<JObject> Created => changeLookup[ChangeType.Create];
        public IEnumerable<JObject> Updated => changeLookup[ChangeType.Update];
        public IEnumerable<JObject> Deleted => changeLookup[ChangeType.Delete];

        public StorageChanges(long token, List<IStorageChange> changes)
        {
            Token = token;
            this.changes = changes;
            this.changeLookup = changes.ToLookup(change => change.Type, change => change.Entity);
            this.count = new Lazy<ChangeCount>(() => new ChangeCount(Created.Count(), Updated.Count(), Deleted.Count()));
        }

        public IEnumerator<IStorageChange> GetEnumerator()
        {
            return changes.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public interface IStorageChange
    {
        long Token { get; }
        ChangeType Type { get; }
        JObject Entity { get; }
    }

    public class StorageChange : IStorageChange
    {
        public long Token { get; }
        public ChangeType Type { get; }
        public JObject Entity => entity.Value;

        private readonly Lazy<JObject> entity;

        public StorageChange(long token, ChangeType type, Lazy<JObject> entity)
        {
            Token = token;
            Type = type;

            this.entity = entity;
        }
    }

    public enum ChangeType
    {
        Create, Update, Delete
    }

    public class SqlServerStorageAreaLog : IStorageAreaLog
    {
        private bool initialized;
        private long previousToken = -1;
        private readonly SqlServerStorageArea area;
        private readonly SqlServerStorageContext context;
        private readonly object padlock = new object();

        public SqlServerStorageAreaLog(SqlServerStorageArea area, SqlServerStorageContext context)
        {
            this.area = area;
            this.context = context;

            this.indexes = new Lazy<Dictionary<string, IndexDefinition>>(() => LoadIndexes().ToDictionary(def => def.Name));
        }

        public IStorageChanges Insert(Guid id, JObject original, JObject changed, ChangeType action, SqlConnection connection, SqlTransaction transaction)
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

                    return new StorageChanges(token, new List<IStorageChange> { new StorageChange(token, action, new Lazy<JObject>(() => changed)) });

                }

            }
        }

        private StorageChanges RunDataReader(long startToken, SqlDataReader reader)
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

            List<IStorageChange> changes = new List<IStorageChange>();
            long maxToken = startToken;
            while (reader.Read())
            {
                long token = reader.GetInt64(tokenColumn);
                maxToken = Math.Max(maxToken, token);

                ChangeType changeType;
                Enum.TryParse(reader.GetString(actionColumn), out changeType);

                var change = new StorageChange(token, changeType,
                    changeType != ChangeType.Delete
                    ? CreateJson(reader, dataColumn, idColumn, refColumn, versionColumn, contentTypeColumn, createdColumn, updatedColumn)
                    : CreateShell(reader, fidColumn));
                changes.Add(change);
            }
            return new StorageChanges(maxToken, changes);
        }

        private Lazy<JObject> CreateShell(SqlDataReader reader, int fidColumn)
        {
            Guid id = reader.GetGuid(fidColumn);
            return new Lazy<JObject>(() =>
            {
                JObject json = new JObject();
                json[context.Configuration.Fields[JsonField.Id]] = id;
                json[context.Configuration.Fields[JsonField.ContentType]] = "Dummy";
                return json;
            });
        }

        private Lazy<JObject> CreateJson(SqlDataReader reader, int dataColumn, int idColumn, int refColumn, int versionColumn, int contentTypeColumn, int createdColumn, int updatedColumn)
        {
            try
            {
                byte[] data = reader.GetSqlBinary(dataColumn).Value;
                Guid id = reader.GetGuid(idColumn);
                long reference = reader.GetInt64(refColumn);
                string name = area.Name;
                int version = reader.GetInt32(versionColumn);
                string contentType = reader.GetString(contentTypeColumn);
                DateTime created = reader.GetDateTime(createdColumn);
                DateTime updated = reader.GetDateTime(updatedColumn);

                return new Lazy<JObject>(() =>
                {
                    JObject json = context.Serializer.Deserialize(data);
                    json[context.Configuration.Fields[JsonField.Id]] = id;
                    json[context.Configuration.Fields[JsonField.Reference]] = Base36.Encode(reference);
                    json[context.Configuration.Fields[JsonField.Area]] = name;
                    json[context.Configuration.Fields[JsonField.Version]] = version;
                    json[context.Configuration.Fields[JsonField.ContentType]] = contentType;
                    json[context.Configuration.Fields[JsonField.Created]] = DateTime.SpecifyKind(created, DateTimeKind.Utc);
                    json[context.Configuration.Fields[JsonField.Updated]] = DateTime.SpecifyKind(updated, DateTimeKind.Utc);
                    json = area.Migrate(json);
                    return json;
                });
            }
            catch (Exception ex)
            {
                //TODO: (jmd 2015-10-01) This is a horrible way around it, will get fixed when we have better materialization. 
                var json = new JObject();
                json["$exception"] = ex.ToString();
                return new Lazy<JObject>(() => json);
            }
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

        public IStorageChanges Get(bool includeDeletes = true, int count = 5000)
        {
            return Get(previousToken, includeDeletes, count);
        }

        public IStorageChanges Get(long token, bool includeDeletes = true, int count = 5000)
        {
            if (!TableExists)
                return new StorageChanges(-1, new List<IStorageChange>());

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
                        StorageChanges changes = RunDataReader(token, reader);
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