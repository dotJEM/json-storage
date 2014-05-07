using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage
{
    public interface ITableAdapter
    {
        IEnumerable<JObject> Get(params long[] ids);
        IEnumerable<JObject> Get(string contentType, params long[] ids);
        JObject Insert(string contentType, JObject json);
        JObject Update(long id, string contentType, JObject json);
        int Delete(long id);
    }

    public class SqlServerTableAdapter : ITableAdapter
    {
        static internal class Fields
        {
            internal const string Id = "Id";
            internal const string ContentType = "ContentType";
            internal const string Created = "Created";
            internal const string Updated = "Updated";
            internal const string Data = "Data";
        }

        private class Commands
        {
            // ReSharper disable MemberCanBePrivate.Local
            public string Update { get; private set; }
            public string Insert { get; private set; }
            public string Delete { get; private set; }
            public string SelectAll { get; private set; }
            public string SelectAllByContentType { get; private set; }
            public string SelectSingle { get; private set; }
            public string SelectSingleByContentType { get; private set; }
            public string SelectMultiple { get; private set; }
            public string SelectMultipleByContentType { get; private set; }
            public string CreateTable { get; private set; }
            // ReSharper restore MemberCanBePrivate.Local

            public Commands(string table)
            {
                //TODO: make table a Parameter

                Update = string.Format("UPDATE {0} SET [{1}] = @{1}, [{2}] = @{2}, [{3}] = @{3} WHERE [{4}] = @{4};",
                    table, Fields.ContentType, Fields.Updated, Fields.Data, Fields.Id);

                Insert = string.Format("INSERT INTO {0} ([{1}], [{2}], [{3}]) VALUES (@{1}, @{2}, @{3}); SELECT SCOPE_IDENTITY();",
                    table, Fields.ContentType, Fields.Created, Fields.Data);

                Delete = string.Format("DELETE FROM {0} WHERE [{1}] = @{1};", table, Fields.Id);

                SelectAllByContentType = string.Format("SELECT * FROM {0} WHERE [{1}] = @{1} ORDER BY [{2}];",
                    table, Fields.ContentType, Fields.Created);

                SelectAll = string.Format("SELECT * FROM {0} ORDER BY [{1}];",
                    table, Fields.Created);

                SelectSingleByContentType = string.Format("SELECT * FROM {0} WHERE [{1}] = @{1} AND [{2}] = @{2} ORDER BY [{3}];",
                    table, Fields.Id, Fields.ContentType, Fields.Created);

                SelectSingle = string.Format("SELECT * FROM {0} WHERE [{1}] = @{1} ORDER BY [{2}];",
                    table, Fields.Id, Fields.Created);

                SelectMultipleByContentType = string.Format("SELECT * FROM {0} WHERE [{1}] = @{1} AND [{2}] IN ($IDS;) ORDER BY [{3}];",
                    table, Fields.ContentType, Fields.Id, Fields.Created);

                SelectMultiple = string.Format("SELECT * FROM {0} WHERE [{1}] IN ($IDS;) ORDER BY [{2}];",
                    table, Fields.Id, Fields.Created);

                CreateTable = string.Format(
                    @"IF (NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES
                                       WHERE TABLE_SCHEMA = 'dbo'
                                         AND TABLE_NAME = '{0}') )
                      BEGIN
                        CREATE TABLE [dbo].[{0}] (
                          [{1}] [bigint] IDENTITY(1,1) NOT NULL,
                          [{2}] [varchar](256) NOT NULL,
                          [{3}] [datetime] NOT NULL,
                          [{4}] [datetime] NULL,
                          [{5}] [varbinary](max) NOT NULL,
                          [Version] [timestamp] NOT NULL,
                          CONSTRAINT [PK_{0}] PRIMARY KEY NONCLUSTERED (
                            [Id] ASC
                          ) WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
                        ) ON [PRIMARY];


                        CREATE CLUSTERED INDEX [IX_{0}_{2}] ON [dbo].[{0}] (
                          [{2}] ASC
                        ) WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY];

                      END;
                    "
                    , table ,Fields.Id, Fields.ContentType, Fields.Created, Fields.Updated, Fields.Data);
            }


            //IF (NOT EXISTS (SELECT * 
            //                 FROM INFORMATION_SCHEMA.TABLES 
            //                 WHERE TABLE_SCHEMA = 'TheSchema' 
            //                 AND  TABLE_NAME = 'TheTable'))
            //BEGIN
            //    --Do Stuff
            //END

            // CREATE TABLE [dbo].[Content](
            //     [Id] [bigint] IDENTITY(1,1) NOT NULL,
            //     [ContentType] [varchar](256) NOT NULL,
            //     [Created] [datetime] NOT NULL,
            //     [Updated] [datetime] NULL,
            //     [Data] [varbinary](max) NOT NULL,
            //     [Version] [timestamp] NOT NULL,
            //  CONSTRAINT [PK_Content] PRIMARY KEY NONCLUSTERED 
            // (
            //     [Id] ASC
            // )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
            // ) ON [PRIMARY]
            // GO
            // SET ANSI_PADDING OFF
            // GO
            // CREATE CLUSTERED INDEX [IX_Content_ContentType] ON [dbo].[Content] 
            // (
            //     [ContentType] ASC
            // )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
            // GO

            public string Select(string contentType, ICollection<long> ids)
            {
                if (!string.IsNullOrEmpty(contentType))
                {
                    return InternalSelectWithContentType(ids);
                }

                switch (ids.Count)
                {
                    case 0: return SelectAll;
                    case 1: return SelectSingle;
                    default: return SelectMultiple.Replace("$IDS;", IdsToString(ids));
                }
            }

            private string InternalSelectWithContentType(ICollection<long> ids)
            {
                switch (ids.Count)
                {
                    case 0: return SelectAllByContentType;
                    case 1: return SelectSingleByContentType;
                    default: return SelectMultipleByContentType.Replace("$IDS;", IdsToString(ids));
                }
            }

            private static string IdsToString(IEnumerable<long> ids)
            {
                return string.Join(",", ids.Select(id => id.ToString(CultureInfo.InvariantCulture)));
            }
        }

        private readonly SqlServerStorageContext context;
        private readonly IBsonSerializer serializer = new BsonSerializer();

        private readonly Commands commands;

        public SqlServerTableAdapter(SqlServerStorageContext context, string tableName)
        {
            this.context = context;
            using (var conn = context.Connection())
            {
                commands = new Commands(string.Format("[{0}].[dbo].[{1}]", conn.Database, tableName));
            }
        }

        public IEnumerable<JObject> Get(params long[] ids)
        {
            return Get(null, ids);
        }

        public IEnumerable<JObject> Get(string contentType, params long[] ids)
        {
            switch (ids.Length)
            {
                case 0:
                    if (!string.IsNullOrEmpty(contentType))
                    {
                        return
                            InternalGet(
                                commands.Select(contentType, ids),
                                new SqlParameter(Fields.ContentType, contentType));
                    }
                    return InternalGet(commands.Select(contentType, ids));

                case 1:
                    if (!string.IsNullOrEmpty(contentType))
                    {
                        return
                            InternalGet(
                                commands.Select(contentType, ids),
                                new SqlParameter(Fields.Id, ids.Single()),
                                new SqlParameter(Fields.ContentType, contentType));
                    }
                    return InternalGet(commands.SelectSingle,
                        new SqlParameter(Fields.Id, ids.Single()));

                //Note: This is a list of Integers, it is rather unlikely that we will suffer from injection attacks.
                //      this would have to involve an overwrite of the InvarianCulture and the ToString for int on that, if that is even possible? o.O.
                //
                //  Besides, we might just wan't to simplify this and use Lucene instead for multi doc retrieval.

                default:
                    if (!string.IsNullOrEmpty(contentType))
                    {
                        return InternalGet(commands.Select(contentType, ids),
                            new SqlParameter(Fields.ContentType, contentType));
                    }
                    return InternalGet(commands.Select(contentType, ids));
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

                    SqlDataReader reader = command.ExecuteReader();
                    int dataColumn = reader.GetOrdinal(Fields.Data);
                    int idColumn = reader.GetOrdinal(Fields.Id);
                    int contentTypeColumn = reader.GetOrdinal(Fields.ContentType);
                    int createdColumn = reader.GetOrdinal(Fields.Created);
                    int updatedColumn = reader.GetOrdinal(Fields.Updated);

                    while (reader.Read())
                    {
                        JObject json = serializer.Deserialize(reader.GetSqlBinary(dataColumn).Value);
                        json[context.Config.Fields.Id] = reader.GetInt64(idColumn);
                        json[context.Config.Fields.ContentType] = reader.GetString(contentTypeColumn);
                        json[context.Config.Fields.Created] = reader.GetDateTime(createdColumn);
                        json[context.Config.Fields.Updated] = !reader.IsDBNull(updatedColumn) ? (DateTime?)reader.GetDateTime(updatedColumn) : null;
                        yield return json;
                    }
                }
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
                    command.CommandText = commands.Insert;
                    command.Parameters.Add(new SqlParameter(Fields.ContentType, SqlDbType.VarChar)).Value = contentType;
                    command.Parameters.Add(new SqlParameter(Fields.Created, SqlDbType.DateTime)).Value = created;
                    command.Parameters.Add(new SqlParameter(Fields.Data, SqlDbType.VarBinary)).Value = serializer.Serialize(json);

                    long id = Convert.ToInt64(command.ExecuteScalar());
                    return Get(contentType, id).Single();
                }
            }
        }

        public JObject Update(long id, string contentType, JObject json)
        {
            using (SqlConnection connection = context.Connection())
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand { Connection = connection })
                {
                    ClearMetaData(json);

                    DateTime updated = DateTime.Now;
                    command.CommandText = commands.Update;
                    command.Parameters.Add(new SqlParameter(Fields.ContentType, SqlDbType.VarChar)).Value = contentType;
                    command.Parameters.Add(new SqlParameter(Fields.Updated, SqlDbType.DateTime)).Value = updated;
                    command.Parameters.Add(new SqlParameter(Fields.Data, SqlDbType.VarBinary)).Value = serializer.Serialize(json);
                    command.Parameters.Add(new SqlParameter(Fields.Id, SqlDbType.BigInt)).Value = id;
                    command.ExecuteScalar();
                    return Get(contentType, id).Single();
                }
            }
        }

        private void ClearMetaData(JObject json)
        {

            //Note: Don't double store these values.
            json.Remove(context.Config.Fields.Id);
            json.Remove(context.Config.Fields.ContentType);
            json.Remove(context.Config.Fields.Created);
            json.Remove(context.Config.Fields.Updated);
        }

        public int Delete(long id)
        {
            using (SqlConnection connection = context.Connection())
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand { Connection = connection })
                {
                    command.CommandText = commands.Delete;
                    command.Parameters.Add(new SqlParameter(Fields.Id, SqlDbType.BigInt)).Value = id;
                    return command.ExecuteNonQuery();
                }
            }
        }


        public bool EnsureTable()
        {
            using (SqlConnection connection = context.Connection())
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand { Connection = connection })
                {
                    command.CommandText = commands.CreateTable;
                    command.ExecuteNonQuery();

                }
            }

            return false;
        }
    }
}

//IF (EXISTS (SELECT * 
//                 FROM INFORMATION_SCHEMA.TABLES 
//                 WHERE TABLE_SCHEMA = 'TheSchema' 
//                 AND  TABLE_NAME = 'TheTable'))
//BEGIN
//    --Do Stuff
//END

// CREATE TABLE [dbo].[Content](
//     [Id] [bigint] IDENTITY(1,1) NOT NULL,
//     [ContentType] [varchar](256) NOT NULL,
//     [Created] [datetime] NOT NULL,
//     [Updated] [datetime] NULL,
//     [Data] [varbinary](max) NOT NULL,
//     [Version] [timestamp] NOT NULL,
//  CONSTRAINT [PK_Content] PRIMARY KEY NONCLUSTERED 
// (
//     [Id] ASC
// )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
// ) ON [PRIMARY]
// GO
// SET ANSI_PADDING OFF
// GO
// CREATE CLUSTERED INDEX [IX_Content_ContentType] ON [dbo].[Content] 
// (
//     [ContentType] ASC
// )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
// GO