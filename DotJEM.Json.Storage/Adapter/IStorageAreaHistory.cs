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
    public interface IStorageAreaHistory
    {
        /// <summary>
        /// Returns true if History is enabled recording is enabled.
        /// </summary>
        bool Enabled { get; }

        /// <summary>
        /// Returns true if Historical data exists even though <see cref="Enabled"/> are false, this could be because that history has been disabled at some point.
        /// </summary>
        bool HasHistory { get; }

        JObject Get(Guid guid, int version);
        IEnumerable<JObject> Get(Guid guid, DateTime? from = null, DateTime? to = null);
        IEnumerable<JObject> GetDeleted(string contentType, DateTime? from = null, DateTime? to = null);
    }

    public abstract class AbstractSqlServerStorageAreaHistory : IStorageAreaHistory
    {
        private bool initialized;
        private Lazy<bool> tableExists;

        protected SqlServerStorageArea Area { get; }
        protected SqlServerStorageContext Context { get; }
        protected bool TableExists => tableExists.Value;

        public bool Enabled => Area.HistoryEnabled;
        public bool HasHistory => TableExists;

        protected AbstractSqlServerStorageAreaHistory(SqlServerStorageArea area, SqlServerStorageContext context)
        {
            this.Area = area;
            this.Context = context;

            ResetTableExistsEvaluator();
        }

        private void ResetTableExistsEvaluator()
        {
            this.tableExists = new Lazy<bool>(() =>
            {
                using (SqlConnection connection = Context.Connection())
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand { Connection = connection })
                    {
                        command.CommandText = Area.Commands["HistoryTableExists"];
                        object result = command.ExecuteScalar();
                        return 1 == Convert.ToInt32(result);
                    }
                }
            });
        }

        public virtual JObject Get(Guid guid, int version)
        {
            if (!TableExists)
                return null;

            return InternalGet("SelectHistoryForByVersion",
                new SqlParameter(HistoryField.Fid.ToString(), guid),
                new SqlParameter(StorageField.Version.ToString(), version))
                .SingleOrDefault();
        }

        public virtual IEnumerable<JObject> Get(Guid guid, DateTime? @from = null, DateTime? to = null)
        {
            if (!TableExists)
                return Enumerable.Empty<JObject>();

            if (@from.HasValue)
            {
                if (to.HasValue)
                {

                    return InternalGet("SelectHistoryForBetweenDate",
                        new SqlParameter(HistoryField.Fid.ToString(), guid),
                        new SqlParameter("fromdate", @from.Value),
                        new SqlParameter("todate", to.Value));
                }

                return InternalGet("SelectHistoryForFromDate",
                    new SqlParameter(HistoryField.Fid.ToString(), guid),
                    new SqlParameter(StorageField.Updated.ToString(), @from.Value));
            }

            if (to.HasValue)
            {

                return InternalGet("SelectHistoryForToDate",
                    new SqlParameter(HistoryField.Fid.ToString(), guid),
                    new SqlParameter(StorageField.Updated.ToString(), to.Value));
            }

            return InternalGet("SelectHistoryFor",
                new SqlParameter(HistoryField.Fid.ToString(), guid));
        }

        public virtual IEnumerable<JObject> GetDeleted(string contentType, DateTime? @from = null, DateTime? to = null)
        {
            if (!TableExists)
                return Enumerable.Empty<JObject>();

            if (@from.HasValue)
            {
                return InternalGet("SelectDeletedHistoryByContentTypeFromDate",
                    new SqlParameter(StorageField.ContentType.ToString(), contentType),
                    new SqlParameter(StorageField.Updated.ToString(), @from.Value));
            }
            return InternalGet("SelectDeletedHistoryByContentType",
                new SqlParameter(StorageField.ContentType.ToString(), contentType));
        }

        public virtual void Create(JObject json, bool deleted, SqlConnection connection, SqlTransaction transaction)
        {
        }

        private IEnumerable<JObject> InternalGet(string cmd, params SqlParameter[] parameters)
        {
            using (SqlConnection connection = Context.Connection())
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand(Area.Commands[cmd], connection))
                {
                    command.CommandTimeout = Context.Configuration.ReadCommandTimeout;
                    command.Parameters.AddRange(parameters);

                    //TODO: Dynamically read columns.
                    foreach (JObject json in RunDataReader(command.ExecuteReader()))
                        yield return json;

                    command.Parameters.Clear();
                }
            }
        }

        private IEnumerable<JObject> RunDataReader(SqlDataReader reader)
        {
            int dataColumn = reader.GetOrdinal(StorageField.Data.ToString());
            int refColumn = reader.GetOrdinal(StorageField.Reference.ToString());
            int fidColumn = reader.GetOrdinal(HistoryField.Fid.ToString());
            int versionColumn = reader.GetOrdinal(StorageField.Version.ToString());
            int contentTypeColumn = reader.GetOrdinal(StorageField.ContentType.ToString());
            int createdColumn = reader.GetOrdinal(StorageField.Created.ToString());
            int updatedColumn = reader.GetOrdinal(StorageField.Updated.ToString());
            while (reader.Read())
            {
                yield return CreateJson(reader, dataColumn, fidColumn, refColumn, versionColumn, contentTypeColumn, createdColumn, updatedColumn);
            }
        }

        private JObject CreateJson(SqlDataReader reader, int dataColumn, int idColumn, int refColumn, int versionColumn, int contentTypeColumn, int createdColumn, int updatedColumn)
        {
            JObject json;
            json = Context.Serializer.Deserialize(reader.GetSqlBinary(dataColumn).Value);
            json[Context.Configuration.Fields[JsonField.Id]] = reader.GetGuid(idColumn);
            json[Context.Configuration.Fields[JsonField.Reference]] = Base36.Encode(reader.GetInt64(refColumn));
            json[Context.Configuration.Fields[JsonField.Area]] = Area.Name;
            json[Context.Configuration.Fields[JsonField.Version]] = reader.GetInt32(versionColumn);
            json[Context.Configuration.Fields[JsonField.ContentType]] = reader.GetString(contentTypeColumn);
            json[Context.Configuration.Fields[JsonField.Created]] = DateTime.SpecifyKind(reader.GetDateTime(createdColumn), DateTimeKind.Utc);
            json[Context.Configuration.Fields[JsonField.Updated]] = DateTime.SpecifyKind(reader.GetDateTime(updatedColumn), DateTimeKind.Utc);
            return json;
        }

        protected virtual void EnsureTable()
        {
            if (initialized)
                return;

            if (!TableExists)
                CreateTable();

            initialized = true;
            ResetTableExistsEvaluator();
        }

        protected virtual void CreateTable() { }
    }

    public class ReadOnlySqlServerStorageAreaHistory : AbstractSqlServerStorageAreaHistory
    {
        public ReadOnlySqlServerStorageAreaHistory(SqlServerStorageArea area, SqlServerStorageContext context) : base(area, context)
        {
        }
    }

    public class SqlServerStorageAreaHistory : AbstractSqlServerStorageAreaHistory
    {
        //private bool initialized;
        //private readonly SqlServerStorageArea area;
        //private readonly SqlServerStorageContext context;

        private readonly object padlock = new object();

        public SqlServerStorageAreaHistory(SqlServerStorageArea area, SqlServerStorageContext context) 
            : base(area, context)
        {
            //this.area = area;
            //this.context = context;
        }

        //public JObject Get(Guid guid, int version)
        //{
        //    if (!TableExists)
        //        return null;

        //    return InternalGet("SelectHistoryForByVersion",
        //        new SqlParameter(HistoryField.Fid.ToString(), guid),
        //        new SqlParameter(StorageField.Version.ToString(), version))
        //        .SingleOrDefault();
        //}

        //public IEnumerable<JObject> Get(Guid guid, DateTime? @from = null, DateTime? to = null)
        //{
        //    if (!TableExists)
        //        return Enumerable.Empty<JObject>();

        //    if (@from.HasValue)
        //    {
        //        if (to.HasValue)
        //        {

        //            return InternalGet("SelectHistoryForBetweenDate",
        //                new SqlParameter(HistoryField.Fid.ToString(), guid),
        //                new SqlParameter("fromdate", @from.Value),
        //                new SqlParameter("todate", to.Value));
        //        }

        //        return InternalGet("SelectHistoryForFromDate",
        //            new SqlParameter(HistoryField.Fid.ToString(), guid),
        //            new SqlParameter(StorageField.Updated.ToString(), @from.Value));
        //    }

        //    if (to.HasValue)
        //    {

        //        return InternalGet("SelectHistoryForToDate",
        //            new SqlParameter(HistoryField.Fid.ToString(), guid),
        //            new SqlParameter(StorageField.Updated.ToString(), to.Value));
        //    }

        //    return InternalGet("SelectHistoryFor",
        //        new SqlParameter(HistoryField.Fid.ToString(), guid));
        //}

        //public IEnumerable<JObject> GetDeleted(string contentType, DateTime? @from = null, DateTime? to = null)
        //{
        //    if (!TableExists)
        //        return Enumerable.Empty<JObject>();

        //    if (@from.HasValue)
        //    {
        //        return InternalGet("SelectDeletedHistoryByContentTypeFromDate",
        //            new SqlParameter(StorageField.ContentType.ToString(), contentType),
        //            new SqlParameter(StorageField.Updated.ToString(), @from.Value));
        //    }
        //    return InternalGet("SelectDeletedHistoryByContentType",
        //        new SqlParameter(StorageField.ContentType.ToString(), contentType));
        //}

        public override void Create(JObject json, bool deleted, SqlConnection connection, SqlTransaction transaction)
        {
            var fields = Context.Configuration.Fields;
            Guid guid = json[fields[JsonField.Id]].ToObject<Guid>();
            string reference = json[fields[JsonField.Reference]].ToObject<string>();
            int version = json[fields[JsonField.Version]].ToObject<int>();
            string contentType = json[fields[JsonField.ContentType]].ToObject<string>();
            DateTime created = json[fields[JsonField.Created]].ToObject<DateTime>();
            DateTime updated = json[fields[JsonField.Updated]].ToObject<DateTime>();

            json = ExecuteDecorators(json);

            EnsureTable();
            
            using (SqlCommand command = new SqlCommand { Connection = connection, Transaction = transaction})
            {
                command.CommandText = Area.Commands["InsertHistory"];
                command.Parameters.Add(new SqlParameter(HistoryField.Fid.ToString(), SqlDbType.UniqueIdentifier)).Value = guid;
                command.Parameters.Add(new SqlParameter(StorageField.Reference.ToString(), SqlDbType.BigInt)).Value = Base36.Decode(reference);
                command.Parameters.Add(new SqlParameter(StorageField.Version.ToString(), SqlDbType.Int)).Value = version;
                command.Parameters.Add(new SqlParameter(StorageField.ContentType.ToString(), SqlDbType.VarChar)).Value = contentType;
                command.Parameters.Add(new SqlParameter(HistoryField.Deleted.ToString(), SqlDbType.Bit)).Value = deleted;
                command.Parameters.Add(new SqlParameter(StorageField.Created.ToString(), SqlDbType.DateTime)).Value = created;
                command.Parameters.Add(new SqlParameter(StorageField.Updated.ToString(), SqlDbType.DateTime)).Value = updated;
                command.Parameters.Add(new SqlParameter(StorageField.Data.ToString(), SqlDbType.VarBinary)).Value = Context.Serializer.Serialize(json);
                command.ExecuteNonQuery();
            }
        }

        //private IEnumerable<JObject> InternalGet(string cmd, params SqlParameter[] parameters)
        //{
        //    EnsureTable();

        //    using (SqlConnection connection = Context.Connection())
        //    {
        //        connection.Open();
        //        using (SqlCommand command = new SqlCommand(Area.Commands[cmd], connection))
        //        {
        //            command.CommandTimeout = Context.Configuration.ReadCommandTimeout;
        //            command.Parameters.AddRange(parameters);

        //            //TODO: Dynamically read columns.
        //            foreach (JObject json in RunDataReader(command.ExecuteReader()))
        //                yield return json;

        //            command.Parameters.Clear();
        //        }
        //    }
        //}

        //private IEnumerable<JObject> RunDataReader(SqlDataReader reader)
        //{
        //    int dataColumn = reader.GetOrdinal(StorageField.Data.ToString());
        //    int refColumn = reader.GetOrdinal(StorageField.Reference.ToString());
        //    int fidColumn = reader.GetOrdinal(HistoryField.Fid.ToString());
        //    int versionColumn = reader.GetOrdinal(StorageField.Version.ToString());
        //    int contentTypeColumn = reader.GetOrdinal(StorageField.ContentType.ToString());
        //    int createdColumn = reader.GetOrdinal(StorageField.Created.ToString());
        //    int updatedColumn = reader.GetOrdinal(StorageField.Updated.ToString());
        //    while (reader.Read())
        //    {
        //        yield return CreateJson(reader, dataColumn, fidColumn, refColumn, versionColumn, contentTypeColumn, createdColumn, updatedColumn);
        //    }
        //}

        //private JObject CreateJson(SqlDataReader reader, int dataColumn, int idColumn, int refColumn,  int versionColumn, int contentTypeColumn, int createdColumn, int updatedColumn)
        //{
        //    JObject json;
        //    json = Context.Serializer.Deserialize(reader.GetSqlBinary(dataColumn).Value);
        //    json[Context.Configuration.Fields[JsonField.Id]] = reader.GetGuid(idColumn);
        //    json[Context.Configuration.Fields[JsonField.Reference]] = Base36.Encode(reader.GetInt64(refColumn));
        //    json[Context.Configuration.Fields[JsonField.Area]] = Area.Name;
        //    json[Context.Configuration.Fields[JsonField.Version]] = reader.GetInt32(versionColumn);
        //    json[Context.Configuration.Fields[JsonField.ContentType]] = reader.GetString(contentTypeColumn);
        //    json[Context.Configuration.Fields[JsonField.Created]] = DateTime.SpecifyKind(reader.GetDateTime(createdColumn), DateTimeKind.Utc);
        //    json[Context.Configuration.Fields[JsonField.Updated]] = DateTime.SpecifyKind(reader.GetDateTime(updatedColumn), DateTimeKind.Utc);
        //    return json;
        //}

        private JObject ExecuteDecorators(JObject json)
        {
            IHistoryEnabledStorageAreaConfiguration config = (IHistoryEnabledStorageAreaConfiguration)Context.Configuration.Area(Area.Name);
            return config.Decorators.Aggregate(json, (obj, decorator) => decorator.Decorate(obj));
        }

        protected override void CreateTable()
        {
            lock (padlock)
            {
                if (TableExists)
                    return;

                using (SqlConnection connection = Context.Connection())
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand { Connection = connection })
                    {
                        command.CommandText = Area.Commands["CreateHistoryTable"];
                        command.ExecuteNonQuery();
                    }
                }
            }
        }
    }
}