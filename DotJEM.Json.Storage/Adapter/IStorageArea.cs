using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using DotJEM.Json.Storage.Adapter.Materialize.ChanceLog;
using DotJEM.Json.Storage.Adapter.Materialize.Log;
using DotJEM.Json.Storage.Configuration;
using DotJEM.Json.Storage.Migration;
using DotJEM.Json.Storage.Queries;
using DotJEM.Json.Storage.Validation;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Adapter
{
    public interface IStorageArea
    {
        string Name { get; }
        bool HistoryEnabled { get; }
        IStorageAreaLog Log { get; }
        IStorageAreaHistory History { get; }
        IEnumerable<JObject> Get();
        IEnumerable<JObject> Get(long rowindex, int count = 100);
        IEnumerable<JObject> Get(string contentType);
        IEnumerable<JObject> Get(string contentType, long rowindex, int count = 100);
        JObject Get(Guid guid);
        JObject Insert(string contentType, JObject json);
        JObject Update(Guid guid, JObject json);
        JObject Delete(Guid guid);

        long Count(string contentType = null);
    }

    public class SqlServerStorageArea : IStorageArea
    {
        private bool initialized;
        private readonly SqlServerStorageContext context;
        private readonly StorageMigrationManager migration;
        private readonly SqlServerStorageAreaHistory history;
        private readonly SqlServerStorageAreaLog log;

        private readonly object padlock = new object();

        public string Name { get; }
        public bool HistoryEnabled { get; }

        public IStorageAreaLog Log => log;


        public IStorageAreaHistory History
        {
            get
            {
                if (history == null)
                    throw new InvalidOperationException("History must be enabled for an area in order to query history data.");

                return history;
            }
        }

        internal ICommandFactory Commands { get; }

        public SqlServerStorageArea(SqlServerStorageContext context, string name, StorageMigrationManager migration)
        {
            Name = name;
            Validator.ValidateArea(name);

            this.context = context;
            this.migration = migration;
            using (var conn = context.Connection())
            {
                Commands = new SqlServerCommandFactory(conn.Database, name);
            }

            log = new SqlServerStorageAreaLog(this, context);

            // ReSharper disable once AssignmentInConditionalExpression
            if (HistoryEnabled = context.Configuration[name].HistoryEnabled)
            {
                history = new SqlServerStorageAreaHistory(this, context);
            }
        }


        public IEnumerable<JObject> Get()
        {
            return InternalGet("SelectAll");
        }

        public IEnumerable<JObject> Get(long rowindex, int count = 100)
        {
            return InternalGet("SelectAllPaged",
                new SqlParameter("rowstart", rowindex),
                new SqlParameter("rowend", rowindex + count)
                );
        }

        public IEnumerable<JObject> Get(string contentType)
        {
            if (contentType == null)
                throw new ArgumentNullException(nameof(contentType));

            return InternalGet("SelectAllByContentType",
                new SqlParameter(StorageField.ContentType.ToString(), contentType));
        }

        public IEnumerable<JObject> Get(string contentType, long rowindex, int count = 100)
        {
            if (contentType == null)
                throw new ArgumentNullException(nameof(contentType));

            return InternalGet("SelectAllPagedByContentType",
                new SqlParameter(StorageField.ContentType.ToString(), contentType),
                new SqlParameter("rowstart", rowindex),
                new SqlParameter("rowend", rowindex + count));
        }



        public JObject Get(Guid guid)
        {
            return InternalGet("SelectSingle",
                new SqlParameter(StorageField.Id.ToString(), guid))
                .SingleOrDefault();
        }

        public JObject Insert(string contentType, JObject json)
        {
            EnsureTable();

            JObject jsonWithMetadata = (JObject)json.DeepClone();
            jsonWithMetadata[context.Configuration.Fields[JsonField.SchemaVersion]] = context.Configuration.VersionProvider.Current;

            using (SqlConnection connection = context.Connection())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadUncommitted))
                {
                    JObject insert;
                    using (SqlCommand command = new SqlCommand { Connection = connection, Transaction = transaction })
                    {
                        DateTime created = DateTime.UtcNow;
                        command.CommandText = Commands["Insert"];
                        command.Parameters.Add(new SqlParameter(StorageField.ContentType.ToString(), SqlDbType.VarChar)).Value = contentType;
                        command.Parameters.Add(new SqlParameter(StorageField.Created.ToString(), SqlDbType.DateTime)).Value = created;
                        command.Parameters.Add(new SqlParameter(StorageField.Updated.ToString(), SqlDbType.DateTime)).Value = created;
                        command.Parameters.Add(new SqlParameter(StorageField.Data.ToString(), SqlDbType.VarBinary)).Value = context.Serializer.Serialize(jsonWithMetadata);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            insert = RunDataReader(reader).Single();
                            reader.Close();
                        }
                    }
                    log.Insert((Guid)insert[context.Configuration.Fields[JsonField.Id]], null, insert, ChangeType.Create, connection, transaction);
                    transaction.Commit();
                    return insert;
                }
            }
        }

        public JObject Update(Guid id, JObject json)
        {
            EnsureTable();

            using (SqlConnection connection = context.Connection())
            {
                connection.Open();
                return InternalUpdate(id, json, connection);
            }
        }

        public long Count(string contentType = null)
        {
            if (!TableExists)
                return 0;

            using (SqlConnection connection = context.Connection())
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand { Connection = connection })
                {
                    if (string.IsNullOrEmpty(contentType))
                    {
                        command.CommandText = Commands["Count"];
                    }
                    else
                    {
                        command.CommandText = Commands["CountByContentType"];
                        command.Parameters.Add(new SqlParameter(StorageField.ContentType.ToString(), contentType));
                    }

                    return (long)command.ExecuteScalar();
                }
            }
        }

        private JObject InternalUpdate(Guid id, JObject json, SqlConnection connection)
        {
            JObject jsonWithMetadata = (JObject)json.DeepClone();
            jsonWithMetadata[context.Configuration.Fields[JsonField.SchemaVersion]] = context.Configuration.VersionProvider.Current;

            using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
            {
                JObject deleted;
                JObject update;
                using (SqlCommand command = new SqlCommand { Connection = connection, Transaction = transaction })
                {
                    DateTime updateTime = DateTime.UtcNow;
                    command.CommandText = Commands["Update"];
                    command.Parameters.Add(new SqlParameter(StorageField.Updated.ToString(), SqlDbType.DateTime)).Value = updateTime;
                    command.Parameters.Add(new SqlParameter(StorageField.Data.ToString(), SqlDbType.VarBinary)).Value = context.Serializer.Serialize(jsonWithMetadata);
                    command.Parameters.Add(new SqlParameter(StorageField.Id.ToString(), SqlDbType.UniqueIdentifier)).Value = id;

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.HasRows)
                            throw new Exception("Unable to update, could not find any existing objects with id '" + id + "'.");

                        reader.Read();

                        deleted = ReadPrefixedRow("DELETED", reader);
                        update = ReadPrefixedRow("INSERTED", reader);

                        reader.Close();
                    }
                }

                log.Insert(id, deleted, update, ChangeType.Update, connection, transaction);
                history?.Create(deleted, false, connection, transaction);

                transaction.Commit();
                return update;
            }
        }

        public JObject Delete(Guid guid)
        {
            EnsureTable();

            using (SqlConnection connection = context.Connection())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadUncommitted))
                {
                    JObject deleted;
                    using (SqlCommand command = new SqlCommand { Connection = connection, Transaction = transaction })
                    {
                        command.CommandText = Commands["Delete"];
                        command.Parameters.Add(new SqlParameter(StorageField.Id.ToString(), SqlDbType.UniqueIdentifier)).Value = guid;

                        //TODO: Transactions (DAMMIT! hoped we could avoid that) - Need to pass the connection around in this case though!.
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            deleted = RunDataReader(reader).SingleOrDefault();
                        }
                    }
                    if (deleted == null)
                        return null;

                    log.Insert(guid, deleted, null, ChangeType.Delete, connection, transaction);
                    history?.Create(deleted, true, connection, transaction);

                    transaction.Commit();
                    return deleted;
                }
            }
        }

        private IEnumerable<JObject> InternalGet(string cmd, params SqlParameter[] parameters)
        {
            EnsureTable();

            using (SqlConnection connection = context.Connection())
            {
                connection.Open();
                List<JObject> entities = GetEntities(cmd, parameters, connection);
                // Migrate must execute after the get/read operation in order not to affect the "get" SQL operation
                entities = MigrateEntities(entities, connection);
                return entities;
            }
        }

        private List<JObject> GetEntities(string cmd, SqlParameter[] parameters, SqlConnection connection)
        {
            List<JObject> entities = new List<JObject>();
            using (SqlCommand command = new SqlCommand(Commands[cmd], connection))
            {
                command.CommandTimeout = context.Configuration.ReadCommandTimeout;
                command.Parameters.AddRange(parameters);

                //TODO: Dynamically read columns.
                using (SqlDataReader dataReader = command.ExecuteReader())
                {
                    entities.AddRange(RunDataReader(dataReader));
                }
                command.Parameters.Clear();
            }
            return entities;
        }

        private List<JObject> MigrateEntities(IEnumerable<JObject> entities, SqlConnection connection)
        {
            return entities.Select(entity => MigrateAndUpdate(entity, connection)).ToList();
        }

        private JObject MigrateAndUpdate(JObject entity, SqlConnection connection)
        {
            var migrated = entity;
            if (migration.Upgrade(ref migrated) && connection != null)
            {
                string idField = context.Configuration.Fields[JsonField.Id];
                migrated = InternalUpdate((Guid)entity[idField], migrated, connection);
            }
            migrated[context.Configuration.Fields[JsonField.SchemaVersion]] = context.Configuration.VersionProvider.Current;
            return migrated;
        }

        internal JObject Migrate(JObject entity)
        {
            return MigrateAndUpdate(entity, null);
        }

        private IEnumerable<JObject> RunDataReader(SqlDataReader reader)
        {
            int dataColumn = reader.GetOrdinal(StorageField.Data.ToString());
            int idColumn = reader.GetOrdinal(StorageField.Id.ToString());
            int refColumn = reader.GetOrdinal(StorageField.Reference.ToString());
            int versionColumn = reader.GetOrdinal(StorageField.Version.ToString());
            int contentTypeColumn = reader.GetOrdinal(StorageField.ContentType.ToString());
            int createdColumn = reader.GetOrdinal(StorageField.Created.ToString());
            int updatedColumn = reader.GetOrdinal(StorageField.Updated.ToString());
            while (reader.Read())
            {
                yield return CreateJson(reader, dataColumn, idColumn, refColumn, versionColumn, contentTypeColumn, createdColumn, updatedColumn);
            }
        }

        private JObject CreateJson(SqlDataReader reader, int dataColumn, int idColumn, int refColumn, int versionColumn, int contentTypeColumn, int createdColumn, int updatedColumn)
        {
            JObject json;
            try
            {
                json = context.Serializer.Deserialize(reader.GetSqlBinary(dataColumn).Value);
            }
            catch (Exception ex)
            {
                json = new JObject();
                json["$exception"] = ex.ToString();
            }
            DateTime dt = new DateTime(DateTime.Now.Ticks, DateTimeKind.Utc);

            json[context.Configuration.Fields[JsonField.Id]] = reader.GetGuid(idColumn);
            json[context.Configuration.Fields[JsonField.Reference]] = Base36.Encode(reader.GetInt64(refColumn));
            json[context.Configuration.Fields[JsonField.Area]] = Name;
            json[context.Configuration.Fields[JsonField.Version]] = reader.GetInt32(versionColumn);
            json[context.Configuration.Fields[JsonField.ContentType]] = reader.GetString(contentTypeColumn);
            json[context.Configuration.Fields[JsonField.Created]] = DateTime.SpecifyKind(reader.GetDateTime(createdColumn), DateTimeKind.Utc);
            json[context.Configuration.Fields[JsonField.Updated]] = DateTime.SpecifyKind(reader.GetDateTime(updatedColumn), DateTimeKind.Utc);
            return json;
        }

        private JObject ReadPrefixedRow(string prefix, SqlDataReader reader)
        {
            int dataColumn = reader.GetOrdinal(prefix + "_" + StorageField.Data);
            int idColumn = reader.GetOrdinal(prefix + "_" + StorageField.Id);
            int refColumn = reader.GetOrdinal(prefix + "_" + StorageField.Reference);
            int versionColumn = reader.GetOrdinal(prefix + "_" + StorageField.Version);
            int contentTypeColumn = reader.GetOrdinal(prefix + "_" + StorageField.ContentType);
            int createdColumn = reader.GetOrdinal(prefix + "_" + StorageField.Created);
            int updatedColumn = reader.GetOrdinal(prefix + "_" + StorageField.Updated);
            return CreateJson(reader, dataColumn, idColumn, refColumn, versionColumn, contentTypeColumn, createdColumn, updatedColumn);
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
                lock (padlock)
                {
                    if (TableExists)
                        return;

                    connection.Open();
                    using (SqlCommand command = new SqlCommand { Connection = connection })
                    {
                        command.CommandText = Commands["CreateTable"];
                        command.ExecuteNonQuery();
                    }
                    using (SqlCommand command = new SqlCommand { Connection = connection })
                    {
                        command.CommandText = Commands["CreateSeedTable"];
                        command.ExecuteNonQuery();
                    }
                }
            }
        }
    }

    public static class Base36
    {
        private static readonly char[] digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

        /// <summary>
        ///     Encode the given number into a Base36 string
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string Encode(long input)
        {
            if (input < 0) throw new ArgumentOutOfRangeException(nameof(input), input, "input cannot be negative");

            Stack<char> result = new Stack<char>();
            while (input != 0)
            {
                result.Push(digits[input % 36]);
                input /= 36;
            }
            return new string(result.ToArray());
        }

        /// <summary>
        ///     Decode the Base36 Encoded string into a number
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static long Decode(string input)
        {
            IEnumerable<char> reversed = input.Reverse();
            long result = 0;
            int pos = 0;
            foreach (char c in reversed)
            {
                result += Array.IndexOf(digits, c) * (long)Math.Pow(36, pos);
                pos++;
            }
            return result;
        }
    }
}
