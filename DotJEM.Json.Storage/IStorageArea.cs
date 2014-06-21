using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using DotJEM.Json.Storage.Queries;
using DotJEM.Json.Storage.Validation;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage
{
    public interface IStorageArea
    {
        bool Initialized { get; }
        bool HistoryExists { get; }

        IEnumerable<JObject> Get(params Guid[] guids);
        IEnumerable<JObject> Get(string contentType, params Guid[] guids);
        JObject Insert(string contentType, JObject json);
        JObject Update(Guid guid, string contentType, JObject json);
        bool Delete(Guid guid);

        bool Initialize();
        bool CreateHistoryTable();
    }

    public interface IStorageAreaHistory
    {
        
    }

    public class SqlServerStorageAreaHistory : IStorageAreaHistory
    {
        
    }

    public class SqlServerStorageArea : IStorageArea
    {
        private readonly IFields fields = new DefaultFields();
        private readonly IBsonSerializer serializer = new BsonSerializer();
        private readonly ICommandFactory commands;

        private readonly SqlServerStorageContext context;

        public SqlServerStorageArea(SqlServerStorageContext context, string areaName)
        {
            Validator.ValidateArea(areaName);

            this.context = context;
            using (var conn = context.Connection())
            {
                commands = new SqlServerCommandFactory(conn.Database, areaName);
            }
        }

        public IEnumerable<JObject> Get(params Guid[] guids)
        {
            return Get(null, guids);
        }

        public IEnumerable<JObject> Get(string contentType, params Guid[] guids)
        {
            switch (guids.Length)
            {
                case 0:
                    if (!string.IsNullOrEmpty(contentType))
                    {
                        return
                            InternalGet(
                                commands.Select(contentType, guids),
                                new SqlParameter(fields.ContentType, contentType));
                    }
                    return InternalGet(commands.Select(contentType, guids));

                case 1:
                    if (!string.IsNullOrEmpty(contentType))
                    {
                        return
                            InternalGet(
                                commands.Select(contentType, guids),
                                new SqlParameter(fields.Id, guids.Single()),
                                new SqlParameter(fields.ContentType, contentType));
                    }
                    return InternalGet(commands.Select(contentType, guids),
                        new SqlParameter(fields.Id, guids.Single()));

                //Note: This is a list of Integers, it is rather unlikely that we will suffer from injection attacks.
                //      this would have to involve an overwrite of the InvarianCulture and the ToString for int on that, if that is even possible? o.O.
                //
                //  Besides, we might just wan't to simplify this and use Lucene instead for multi doc retrieval.

                default:
                    if (!string.IsNullOrEmpty(contentType))
                    {
                        return InternalGet(commands.Select(contentType, guids),
                            new SqlParameter(fields.ContentType, contentType));
                    }
                    return InternalGet(commands.Select(contentType, guids));
            }
        }

        private IEnumerable<JObject> InternalGet(string commandText, params SqlParameter[] parameters)
        {
            using (SqlConnection connection = context.Connection())
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand(commandText, connection))
                {
                    command.Parameters.AddRange(parameters);

                    //TODO: Dynamically read columns.
                    foreach (JObject json in RunDataReader(command.ExecuteReader()))
                        yield return json;
                }
            }
        }

        private IEnumerable<JObject> RunDataReader(SqlDataReader reader)
        {
            int dataColumn = reader.GetOrdinal(fields.Data);
            int idColumn = reader.GetOrdinal(fields.Id);
            int versionColumn = reader.GetOrdinal(fields.Version);
            int contentTypeColumn = reader.GetOrdinal(fields.ContentType);
            int createdColumn = reader.GetOrdinal(fields.Created);
            int updatedColumn = reader.GetOrdinal(fields.Updated);

            while (reader.Read())
            {
                JObject json = serializer.Deserialize(reader.GetSqlBinary(dataColumn).Value);
                json[context.Config.Fields.Id] = reader.GetGuid(idColumn);
                json[context.Config.Fields.Version] = reader.GetInt32(versionColumn);
                json[context.Config.Fields.ContentType] = reader.GetString(contentTypeColumn);
                json[context.Config.Fields.Created] = reader.GetDateTime(createdColumn);
                json[context.Config.Fields.Updated] = !reader.IsDBNull(updatedColumn)
                    ? (DateTime?) reader.GetDateTime(updatedColumn)
                    : null;
                yield return json;
            }
        }

        public JObject Insert(string contentType, JObject json)
        {
            using (SqlConnection connection = context.Connection())
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand { Connection = connection })
                {
                    //Note: Don't double store these values. 
                    //      Here we clear them in case that we wan't to store a copy of an object.
                    ClearMetaData(json);

                    DateTime created = DateTime.Now;
                    command.CommandText = commands["Insert"];
                    command.Parameters.Add(new SqlParameter(fields.ContentType, SqlDbType.VarChar)).Value = contentType;
                    command.Parameters.Add(new SqlParameter(fields.Created, SqlDbType.DateTime)).Value = created;
                    command.Parameters.Add(new SqlParameter(fields.Data, SqlDbType.VarBinary)).Value = serializer.Serialize(json);

                    return RunDataReader(command.ExecuteReader()).Single();
                }
            }
        }

        private JObject ReadPrefixedRow(string prefix, SqlDataReader reader)
        {
            int dataColumn = reader.GetOrdinal(prefix + "_" + fields.Data);
            int idColumn = reader.GetOrdinal(prefix + "_" + fields.Id);
            int versionColumn = reader.GetOrdinal(prefix + "_" + fields.Version);
            int contentTypeColumn = reader.GetOrdinal(prefix + "_" + fields.ContentType);
            int createdColumn = reader.GetOrdinal(prefix + "_" + fields.Created);
            int updatedColumn = reader.GetOrdinal(prefix + "_" + fields.Updated);

                JObject json = serializer.Deserialize(reader.GetSqlBinary(dataColumn).Value);
                json[context.Config.Fields.Id] = reader.GetGuid(idColumn);
                json[context.Config.Fields.Version] = reader.GetInt32(versionColumn);
                json[context.Config.Fields.ContentType] = reader.GetString(contentTypeColumn);
                json[context.Config.Fields.Created] = reader.GetDateTime(createdColumn);
                json[context.Config.Fields.Updated] = !reader.IsDBNull(updatedColumn)
                    ? (DateTime?)reader.GetDateTime(updatedColumn)
                    : null;
                return json;
        }

        public JObject Update(Guid id, string contentType, JObject json)
        {
            using (SqlConnection connection = context.Connection())
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand { Connection = connection })
                {
                    ClearMetaData(json);

                    command.CommandText = commands["Update"];
                    command.Parameters.Add(new SqlParameter(fields.ContentType, SqlDbType.VarChar)).Value = contentType;
                    command.Parameters.Add(new SqlParameter(fields.Updated, SqlDbType.DateTime)).Value = DateTime.Now;
                    command.Parameters.Add(new SqlParameter(fields.Data, SqlDbType.VarBinary)).Value = serializer.Serialize(json);
                    command.Parameters.Add(new SqlParameter(fields.Id, SqlDbType.UniqueIdentifier)).Value = id;

                    SqlDataReader reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        JObject updated = ReadPrefixedRow("INSERTED", reader);
                        //TODO: If history is enabled, store record in history.
                        if (fields.ContentType == "HolyBoly")
                            RecordHistory(id, updated);
                        return ReadPrefixedRow("DELETED", reader);
                    }
                }
            }
            throw new Exception("Unable to update, could not find any existing objects with id '" + id + "'.");
        }

        private void RecordHistory(Guid id, JObject updated)
        {
        }

        private void ClearMetaData(JObject json)
        {
            //Note: Don't double store these values.
            json.Remove(context.Config.Fields.Id);
            json.Remove(context.Config.Fields.ContentType);
            json.Remove(context.Config.Fields.Created);
            json.Remove(context.Config.Fields.Updated);
        }

        public bool Delete(Guid guid)
        {
            using (SqlConnection connection = context.Connection())
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand { Connection = connection })
                {
                    command.CommandText = commands["Delete"];
                    command.Parameters.Add(new SqlParameter(fields.Id, SqlDbType.UniqueIdentifier)).Value = guid;
                    var deleted = RunDataReader(command.ExecuteReader()).SingleOrDefault();
                    if (fields.ContentType == "HolyBoly")
                        RecordHistory(guid, deleted);
                    return deleted != null;
                }
            }
        }

        public bool Initialize()
        {
            if (Initialized)
                return false;

            using (SqlConnection connection = context.Connection())
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand { Connection = connection })
                {
                    command.CommandText = commands["Initialize"];
                    command.ExecuteNonQuery();
                }
            }
            return true;
        }

        public bool Initialized
        {
            get
            {
                using (SqlConnection connection = context.Connection())
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand { Connection = connection })
                    {
                        command.CommandText = commands["TableExists"];
                        object result = command.ExecuteScalar();
                        return 1 == Convert.ToInt32(result);
                    }
                }
            }
        }

        public bool CreateHistoryTable()
        {
            if (HistoryExists)
                return false;

            using (SqlConnection connection = context.Connection())
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand { Connection = connection })
                {
                    command.CommandText = commands["CreateHistoryTable"];
                    command.ExecuteNonQuery();
                }
            }
            return true;
        }

        public bool HistoryExists
        {
            get
            {
                using (SqlConnection connection = context.Connection())
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand { Connection = connection })
                    {
                        command.CommandText = commands["HistoryTableExists"];
                        object result = command.ExecuteScalar();
                        return 1 == Convert.ToInt32(result);
                    }
                }
            }
        }
    }
}