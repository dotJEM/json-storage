using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using DotJEM.Json.Storage.Configuration;
using DotJEM.Json.Storage.Queries;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Adapter
{
    public interface IStorageAreaLog
    {
        IStorageChanges Insert(Guid id, JObject original, JObject changed, LogAction action);
        IStorageChanges Get(long token);
    }

    public interface IStorageChanges
    {
        long Token { get; }
        IEnumerable<JObject> Changes { get; }
    }

    public class StorageChanges : IStorageChanges
    {
        public long Token { get; private set; }
        public IEnumerable<JObject> Changes { get; private set; }

        public StorageChanges(long token, IEnumerable<JObject> changes)
        {
            Token = token;
            Changes = changes;
        }
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

        public IStorageChanges Insert(Guid id, JObject original, JObject changed, LogAction action)
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

                    long token;
                    JObject change = RunSingleDataReader(command.ExecuteReader(), out token);
                    return new StorageChanges(token, new List<JObject> { change });
                }
            }
        }

        private JObject RunSingleDataReader(SqlDataReader reader, out long token)
        {
            int tokenColumn = reader.GetOrdinal(StorageField.Id.ToString());
            int idColumn = reader.GetOrdinal(StorageField.Fid.ToString());
            int dataColumn = reader.GetOrdinal(StorageField.Data.ToString());
            if (reader.Read())
            {
                token = reader.GetInt64(tokenColumn);
                return CreateJson(reader, dataColumn, idColumn);
            }
            token = -1;
            return null;
        }

        private IEnumerable<JObject> RunDataReader(SqlDataReader reader, out long? token)
        {
            token = null;
            int tokenColumn = reader.GetOrdinal(StorageField.Id.ToString());
            int idColumn = reader.GetOrdinal(StorageField.Fid.ToString());
            int dataColumn = reader.GetOrdinal(StorageField.Data.ToString());
            
            List<JObject> changeedObjects = new List<JObject>();
            while (reader.Read())
            {
                token = reader.GetInt64(tokenColumn);
                changeedObjects.Add(CreateJson(reader, dataColumn, idColumn));
            }
            return changeedObjects;
        }

        private JObject CreateJson(SqlDataReader reader, int dataColumn, int idColumn)
        {
            JObject json = context.Serializer.Deserialize(reader.GetSqlBinary(dataColumn).Value);
            json[context.Configuration.Fields[JsonField.Id]] = reader.GetGuid(idColumn);
            json[context.Configuration.Fields[JsonField.Area]] = area.Name;
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
                    command.Parameters.Add(new SqlParameter(StorageField.Id.ToString(), SqlDbType.BigInt)).Value = token;
                    //self.SelectChanges = vars.Format("SELECT * FROM {logTableFullName} WHERE [{id}] > @{id} ORDER BY [{id}] DESC;");

                    long? nextToken;
                    IEnumerable<JObject> changes = RunDataReader(command.ExecuteReader(), out nextToken);
                    return new StorageChanges(nextToken ?? token, changes);
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

    public enum LogAction
    {
        Create, Update, Delete
    }
}