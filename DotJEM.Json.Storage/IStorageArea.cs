using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using DotJEM.Json.Storage.Configuration;
using DotJEM.Json.Storage.Queries;
using DotJEM.Json.Storage.Validation;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage
{
    public interface IStorageArea
    {
        //bool Initialized { get; }
        //bool Initialize();

        IEnumerable<JObject> Get();
        IEnumerable<JObject> Get(string contentType);
        JObject Get(Guid guid);

        JObject Insert(string contentType, JObject json);
        JObject Update(Guid guid, string contentType, JObject json);
        bool Delete(Guid guid);

        //bool HistoryExists { get; }
        //bool CreateHistoryTable();
    }

    public interface IStorageAreaHistory
    {
        //bool Initialized { get; }
        //bool Initialize();

        IEnumerable<JObject> Get(Guid guid, DateTime? from = null, DateTime? to = null);
        IEnumerable<JObject> GetDeleted(string contentType, DateTime? from = null, DateTime? to = null);
        void Create(JObject json, bool deleted);
    }

    public class SqlServerStorageAreaHistory : IStorageAreaHistory
    {
        private bool initialized;
        private readonly SqlServerStorageArea area;
        private readonly SqlServerStorageContext context;

        public SqlServerStorageAreaHistory(SqlServerStorageArea area, SqlServerStorageContext context)
        {
            this.area = area;
            this.context = context;
        }

        public IEnumerable<JObject> Get(Guid guid, DateTime? @from = null, DateTime? to = null)
        {
            EnsureTable();

            throw new NotImplementedException();
        }

        public IEnumerable<JObject> GetDeleted(string contentType, DateTime? @from = null, DateTime? to = null)
        {
            EnsureTable();

            throw new NotImplementedException();
        }

        public void Create(JObject json, bool deleted)
        {
            var fields = context.Configuration.Fields;
            Guid guid = json[fields[JsonField.Id]].ToObject<Guid>();
            int version = json[fields[JsonField.Version]].ToObject<int>();
            string contentType = json[fields[JsonField.ContentType]].ToObject<string>();
            DateTime created = json[fields[JsonField.Created]].ToObject<DateTime>();

            JToken updatedToken = json[fields[JsonField.Updated]];
            object updated = updatedToken.Type == JTokenType.Null ? (object) DBNull.Value : json[fields[JsonField.Updated]].ToObject<DateTime>();

            //Note: Don't double store these values. 
            //      Here we clear them in case that we wan't to store a copy of an object.
            ExecuteDecorators(json);
            ClearMetaData(json);

            EnsureTable();

            using (SqlConnection connection = context.Connection())
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand { Connection = connection })
                {
                    command.CommandText = area.Commands["InsertHistory"];
                    command.Parameters.Add(new SqlParameter(HistoryField.Fid.ToString(), SqlDbType.UniqueIdentifier)).Value = guid;
                    command.Parameters.Add(new SqlParameter(StorageField.Version.ToString(), SqlDbType.Int)).Value = version;
                    command.Parameters.Add(new SqlParameter(StorageField.ContentType.ToString(), SqlDbType.VarChar)).Value = contentType;
                    command.Parameters.Add(new SqlParameter(HistoryField.Deleted.ToString(), SqlDbType.Bit)).Value = deleted;
                    command.Parameters.Add(new SqlParameter(StorageField.Created.ToString(), SqlDbType.DateTime)).Value = created;
                    command.Parameters.Add(new SqlParameter(StorageField.Updated.ToString(), SqlDbType.DateTime)).Value = updated;
                    command.Parameters.Add(new SqlParameter(StorageField.Data.ToString(), SqlDbType.VarBinary)).Value = context.Serializer.Serialize(json);
                    command.ExecuteNonQuery();
                }
            }
        }

        private void ExecuteDecorators(JObject json)
        {
            context.Configuration.Area(area.Name);
        }

        private void ClearMetaData(JObject json)
        {
            var fields = context.Configuration.Fields;
            json.Remove(fields[JsonField.Id]);
            json.Remove(fields[JsonField.Version]);
            json.Remove(fields[JsonField.ContentType]);
            json.Remove(fields[JsonField.Created]);
            json.Remove(fields[JsonField.Updated]);
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
                        command.CommandText = area.Commands["HistoryTableExists"];
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
                connection.Open();
                using (SqlCommand command = new SqlCommand { Connection = connection })
                {
                    command.CommandText = area.Commands["CreateHistoryTable"];
                    command.ExecuteNonQuery();
                }
            }
        }
    }

    public class NullStorageAreaHistory : IStorageAreaHistory
    {
        private IEnumerable<JObject> ThrowError()
        {
            throw new InvalidOperationException("History must be enabled for an area in order to query history data.");
        }

        public bool Initialized { get { return false; } }

        public bool Initialize()
        {
            return ThrowError().Any();
        }

        public IEnumerable<JObject> Get(Guid guid, DateTime? @from = null, DateTime? to = null)
        {
            return ThrowError();
        }

        public IEnumerable<JObject> GetDeleted(string contentType, DateTime? @from = null, DateTime? to = null)
        {
            return ThrowError();
        }

        public void Create(JObject json, bool deleted)
        {

        }
    }

    public class SqlServerStorageArea : IStorageArea
    {
        private bool initialized;
        private readonly SqlServerStorageContext context;
        private readonly IStorageAreaHistory history = new NullStorageAreaHistory();

        public string Name { get; private set; }
        internal ICommandFactory Commands { get; private set; }

        public SqlServerStorageArea(SqlServerStorageContext context, string name)
        {
            Name = name;
            Validator.ValidateArea(name);

            this.context = context;
            using (var conn = context.Connection())
            {
                Commands = new SqlServerCommandFactory(conn.Database, name);
            }

            if (context.Configuration.Area(name).HistoryEnabled)
            {
                history = new SqlServerStorageAreaHistory(this, context);
            }
        }

        public IEnumerable<JObject> Get()
        {
            return InternalGet("SelectAll");
        }

        public IEnumerable<JObject> Get(string contentType)
        {
            if (contentType == null) 
                throw new ArgumentNullException("contentType");

            return InternalGet("SelectAllByContentType",
                new SqlParameter(StorageField.ContentType.ToString(), contentType));
        }

        public JObject Get(Guid guid)
        {
            return InternalGet("SelectSingle", 
                new SqlParameter(StorageField.Id.ToString(), guid))
                .SingleOrDefault();
        }

        private IEnumerable<JObject> InternalGet(string cmd, params SqlParameter[] parameters)
        {
            EnsureTable();

            using (SqlConnection connection = context.Connection())
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand(Commands[cmd], connection))
                {
                    command.Parameters.AddRange(parameters);

                    //TODO: Dynamically read columns.
                    foreach (JObject json in RunDataReader(command.ExecuteReader()))
                        yield return json;
                }
            }
        }

        public JObject Insert(string contentType, JObject json)
        {
            //Note: Don't double store these values. 
            //      Here we clear them in case that we wan't to store a copy of an object.
            ClearMetaData(json);

            EnsureTable();

            using (SqlConnection connection = context.Connection())
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand { Connection = connection })
                {
                    DateTime created = DateTime.Now;
                    command.CommandText = Commands["Insert"];
                    command.Parameters.Add(new SqlParameter(StorageField.ContentType.ToString(), SqlDbType.VarChar)).Value = contentType;
                    command.Parameters.Add(new SqlParameter(StorageField.Created.ToString(), SqlDbType.DateTime)).Value = created;
                    command.Parameters.Add(new SqlParameter(StorageField.Data.ToString(), SqlDbType.VarBinary)).Value = context.Serializer.Serialize(json);

                    return RunDataReader(command.ExecuteReader()).Single();
                }
            }
        }

        public JObject Update(Guid id, string contentType, JObject json)
        {
            //Note: Don't double store these values. 
            //      Here we clear them in case that we wan't to store a copy of an object.
            ClearMetaData(json);

            EnsureTable();

            using (SqlConnection connection = context.Connection())
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand { Connection = connection })
                {

                    DateTime updateTime = DateTime.Now;
                    command.CommandText = Commands["Update"];
                    command.Parameters.Add(new SqlParameter(StorageField.ContentType.ToString(), SqlDbType.VarChar)).Value = contentType;
                    command.Parameters.Add(new SqlParameter(StorageField.Updated.ToString(), SqlDbType.DateTime)).Value = updateTime;
                    command.Parameters.Add(new SqlParameter(StorageField.Data.ToString(), SqlDbType.VarBinary)).Value = context.Serializer.Serialize(json);
                    command.Parameters.Add(new SqlParameter(StorageField.Id.ToString(), SqlDbType.UniqueIdentifier)).Value = id;

                    SqlDataReader reader = command.ExecuteReader();
                    if (!reader.HasRows) 
                        throw new Exception("Unable to update, could not find any existing objects with id '" + id + "'.");

                    reader.Read();

                    history.Create(ReadPrefixedRow("DELETED", reader), false);
                    return ReadPrefixedRow("INSERTED", reader);
                }
            }
        }

        public bool Delete(Guid guid)
        {
            EnsureTable();

            using (SqlConnection connection = context.Connection())
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand { Connection = connection })
                {
                    command.CommandText = Commands["Delete"];
                    command.Parameters.Add(new SqlParameter(StorageField.Id.ToString(), SqlDbType.UniqueIdentifier)).Value = guid;
                    JObject deleted = RunDataReader(command.ExecuteReader()).SingleOrDefault();
                    if (deleted == null) 
                        return false;

                    history.Create(deleted, true);
                    return true;
                }
            }
        }

        internal void ClearMetaData(JObject json)
        {
            var fields = context.Configuration.Fields;
            json.Remove(fields[JsonField.Id]);
            json.Remove(fields[JsonField.Version]);
            json.Remove(fields[JsonField.ContentType]);
            json.Remove(fields[JsonField.Created]);
            json.Remove(fields[JsonField.Updated]);
        }

        private IEnumerable<JObject> RunDataReader(SqlDataReader reader)
        {
            int dataColumn = reader.GetOrdinal(StorageField.Data.ToString());
            int idColumn = reader.GetOrdinal(StorageField.Id.ToString());
            int versionColumn = reader.GetOrdinal(StorageField.Version.ToString());
            int contentTypeColumn = reader.GetOrdinal(StorageField.ContentType.ToString());
            int createdColumn = reader.GetOrdinal(StorageField.Created.ToString());
            int updatedColumn = reader.GetOrdinal(StorageField.Updated.ToString());
            while (reader.Read())
            {
                yield return CreateJson(reader, dataColumn, idColumn, versionColumn, contentTypeColumn, createdColumn, updatedColumn);
            }
        }

        private JObject CreateJson(SqlDataReader reader, int dataColumn, int idColumn, int versionColumn, int contentTypeColumn, int createdColumn, int updatedColumn)
        {
            JObject json;
            json = context.Serializer.Deserialize(reader.GetSqlBinary(dataColumn).Value);
            json[context.Configuration.Fields[JsonField.Id]] = reader.GetGuid(idColumn);
            json[context.Configuration.Fields[JsonField.Version]] = reader.GetInt32(versionColumn);
            json[context.Configuration.Fields[JsonField.ContentType]] = reader.GetString(contentTypeColumn);
            json[context.Configuration.Fields[JsonField.Created]] = reader.GetDateTime(createdColumn);
            json[context.Configuration.Fields[JsonField.Updated]] = !reader.IsDBNull(updatedColumn)
                ? (DateTime?)reader.GetDateTime(updatedColumn)
                : null;
            return json;
        }

        private JObject ReadPrefixedRow(string prefix, SqlDataReader reader)
        {
            int dataColumn = reader.GetOrdinal(prefix + "_" + StorageField.Data);
            int idColumn = reader.GetOrdinal(prefix + "_" + StorageField.Id);
            int versionColumn = reader.GetOrdinal(prefix + "_" + StorageField.Version);
            int contentTypeColumn = reader.GetOrdinal(prefix + "_" + StorageField.ContentType);
            int createdColumn = reader.GetOrdinal(prefix + "_" + StorageField.Created);
            int updatedColumn = reader.GetOrdinal(prefix + "_" + StorageField.Updated);
            return CreateJson(reader, dataColumn, idColumn, versionColumn, contentTypeColumn, createdColumn, updatedColumn);
        }

        private void EnsureTable()
        {
            if(initialized)
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
                        command.CommandText = Commands["TableExists"];
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
                connection.Open();
                using (SqlCommand command = new SqlCommand { Connection = connection })
                {
                    command.CommandText = Commands["CreateTable"];
                    command.ExecuteNonQuery();
                }
            }
        }
    }
}