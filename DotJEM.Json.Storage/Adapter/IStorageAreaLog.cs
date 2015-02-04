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
        IStorageChanges Insert(Guid id, JObject original, JObject changed, ChangeType action);
        IStorageChanges Get(long token);
    }

    public interface IStorageChanges : IEnumerable<IStorageChange>
    {
        long Token { get; }
        IEnumerable<JObject> Creates { get; }
        IEnumerable<JObject> Updates { get; }
        IEnumerable<JObject> Deletes { get; }
    }

    public class StorageChanges : IStorageChanges
    {
        private readonly List<IStorageChange> changes;

        public long Token { get { return changes.Max(change => change.Token); } }

        public IEnumerable<JObject> Creates { get { return changes.Where(c => c.Type == ChangeType.Create).Select(c => c.Entity); } }
        public IEnumerable<JObject> Updates { get { return changes.Where(c => c.Type == ChangeType.Update).Select(c => c.Entity); } }
        public IEnumerable<JObject> Deletes { get { return changes.Where(c => c.Type == ChangeType.Delete).Select(c => c.Entity); } }

        public StorageChanges(List<IStorageChange> changes)
        {
            this.changes = changes;
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
        public long Token { get; private set; }
        public ChangeType Type { get; private set; }
        public JObject Entity { get; private set; }

        public StorageChange(long token, ChangeType type, JObject entity)
        {
            Token = token;
            Type = type;
            Entity = entity;
        }
    }

    public enum ChangeType
    {
        Create, Update, Delete
    }

    public class SqlServerStorageAreaLog : IStorageAreaLog
    {
        private bool initialized;
        private readonly SqlServerStorageArea area;
        private readonly SqlServerStorageContext context;
        private readonly object padlock = new object();

        public SqlServerStorageAreaLog(SqlServerStorageArea area, SqlServerStorageContext context)
        {
            this.area = area;
            this.context = context;
        }

        public IStorageChanges Insert(Guid id, JObject original, JObject changed, ChangeType action)
        {
            EnsureTable();

            using (SqlConnection connection = context.Connection())
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand { Connection = connection })
                {
                    command.CommandText = area.Commands["InsertChange"];
                    command.Parameters.Add(new SqlParameter(StorageField.Fid.ToString(), SqlDbType.UniqueIdentifier)).Value = id;
                    command.Parameters.Add(new SqlParameter(LogField.Action.ToString(), SqlDbType.VarChar)).Value = action.ToString();
                    command.Parameters.Add(new SqlParameter(StorageField.Data.ToString(), SqlDbType.VarBinary)).Value = context.Serializer.Serialize(Diff(original, changed));

                    SqlDataReader reader = command.ExecuteReader();
                    reader.Read();
                    long token = reader.GetInt64(reader.GetOrdinal(StorageField.Id.ToString()));

                    return new StorageChanges(new List<IStorageChange> { new StorageChange(token, action, changed) });
                }
            }
        }

        private IEnumerable<IStorageChange> RunDataReader(SqlDataReader reader)
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

                ChangeType changeType;
                Enum.TryParse(reader.GetString(actionColumn), out changeType);

                yield return new StorageChange(token, changeType,
                    changeType != ChangeType.Delete 
                    ? CreateJson(reader, dataColumn, idColumn, refColumn, versionColumn, contentTypeColumn, createdColumn, updatedColumn)
                    : CreateShell(reader, fidColumn));
            }
        }

        private JObject CreateShell(SqlDataReader reader, int fidColumn)
        {
            JObject json = new JObject();
            json[context.Configuration.Fields[JsonField.Id]] = reader.GetGuid(fidColumn);
            return json;
        }

        private JObject CreateJson(SqlDataReader reader, int dataColumn, int idColumn, int refColumn, int versionColumn, int contentTypeColumn, int createdColumn, int updatedColumn)
        {
            JObject json = context.Serializer.Deserialize(reader.GetSqlBinary(dataColumn).Value);
            json[context.Configuration.Fields[JsonField.Id]] = reader.GetGuid(idColumn);
            json[context.Configuration.Fields[JsonField.Reference]] = Base36.Encode(reader.GetInt64(refColumn));
            json[context.Configuration.Fields[JsonField.Area]] = area.Name;
            json[context.Configuration.Fields[JsonField.Version]] = reader.GetInt32(versionColumn);
            json[context.Configuration.Fields[JsonField.ContentType]] = reader.GetString(contentTypeColumn);
            json[context.Configuration.Fields[JsonField.Created]] = reader.GetDateTime(createdColumn);
            json[context.Configuration.Fields[JsonField.Updated]] = reader.GetDateTime(updatedColumn);
            return json;
        }

        private JObject Diff(JObject original, JObject changed)
        {
            //TODO: Implemnt simple diff (record changed properties)
            //      - Could also use this for change details...
            return new JObject();
        }

        public IStorageChanges Get(long token)
        {
            EnsureTable();
            using (SqlConnection connection = context.Connection())
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand { Connection = connection })
                {
                    command.CommandText = area.Commands["SelectChanges"];
                    command.CommandText = area.Commands["SelectChangedObjectsDestinct"];
                    command.Parameters.Add(new SqlParameter("token", SqlDbType.BigInt)).Value = token;
                    //self.SelectChanges = vars.Format("SELECT * FROM {logTableFullName} WHERE [{id}] > @{id} ORDER BY [{id}] DESC;");

                    IEnumerable<IStorageChange> changes = RunDataReader(command.ExecuteReader());
                    return new StorageChanges(changes.OrderByDescending(change => change.Token).ToList());
                }
            }
        }

        private void EnsureTable()
        {
            if (initialized)
                return;

            if (!TableExists)
                CreateTable();

            initialized = true;
        }

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
    }
}