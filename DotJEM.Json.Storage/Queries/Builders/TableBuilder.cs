using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotJEM.Json.Storage.Queries.Builders
{
    public static class Tester
    {
        public static void Testing()
        {
            SqlDatabaseBuilder db = new SqlDatabaseBuilder("nsw");


            dynamic builder = db.Table("content");

            builder.AddColumn("Id", SqlDbType.UniqueIdentifier).NotNull()
                .AddColumn("Reference", SqlDbType.VarChar)
                .AddColumn("Version", SqlDbType.Int).NotNull()
                .AddColumn("ContentType", SqlDbType.Int).NotNull()
                .AddColumn("Created", SqlDbType.Int).NotNull()
                .AddColumn("Updated", SqlDbType.Int).NotNull()
                .AddColumn("Data", SqlDbType.Int).NotNull();

            //command.CommandText = area.Commands["InsertHistory"];
            //command.Parameters.Add(new SqlParameter(HistoryField.Fid.ToString(), SqlDbType.UniqueIdentifier)).Value = guid;
            //command.Parameters.Add(new SqlParameter(StorageField.Version.ToString(), SqlDbType.Int)).Value = version;
            //command.Parameters.Add(new SqlParameter(StorageField.ContentType.ToString(), SqlDbType.VarChar)).Value = contentType;
            //command.Parameters.Add(new SqlParameter(HistoryField.Deleted.ToString(), SqlDbType.Bit)).Value = deleted;
            //command.Parameters.Add(new SqlParameter(StorageField.Created.ToString(), SqlDbType.DateTime)).Value = created;
            //command.Parameters.Add(new SqlParameter(StorageField.Updated.ToString(), SqlDbType.DateTime)).Value = updated;
            //command.Parameters.Add(new SqlParameter(StorageField.Data.ToString(), SqlDbType.VarBinary)).Value = context.Serializer.Serialize(json);
        }
    }


    public class SqlServerTableBuilder 
    {
        private readonly SqlDatabaseBuilder db;
        private readonly string name;
        private readonly string schema;

        public SqlServerTableBuilder(SqlDatabaseBuilder db, string name, string schema = "dbo")
        {
            this.db = db;
            this.name = name;
            this.schema = schema;
        }

        public SqlServerTableBuilder Column()
        {
            return this;
        }
    }

    public class SqlDatabaseBuilder
    {
        private readonly string nsw;
        private readonly string schema;

        public SqlDatabaseBuilder(string nsw, string schema = "dbo")
        {
            this.nsw = nsw;
            this.schema = schema;
        }

        public SqlServerTableBuilder Table(string name)
        {
            return Table(name, schema);
        }

        public SqlServerTableBuilder Table(string name, string tableSchema)
        {
            return new SqlServerTableBuilder(this, name, tableSchema);
        }
    }
}
