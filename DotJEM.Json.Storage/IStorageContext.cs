using System.Data.SqlClient;

namespace DotJEM.Json.Storage
{
    public interface IFieldNames
    {
        string Id { get; set; }
        string ContentType { get; set; }
        string Created { get; set; }
        string Updated { get; set; }
    }

    public interface IConfiguration
    {
        IFieldNames Fields { get; }
    }

    public class Configuration : IConfiguration, IFieldNames
    {
        public IFieldNames Fields
        {
            get { return this; }
        }

        public string Id { get; set; }
        public string ContentType { get; set; }
        public string Created { get; set; }
        public string Updated { get; set; }

        public Configuration()
        {
            Id = "_id";
            ContentType = "_contentType";
            Created = "_created";
            Updated = "_updated";
        }
    }

    public interface IStorageContext
    {
        IConfiguration Config { get; }
        ITableAdapter Area(string name = "Content");
    }

    public class SqlServerStorageContext : IStorageContext
    {
        private readonly string connectionString;

        public IConfiguration Config { get; private set; }

        public SqlServerStorageContext(string connectionString)
        {
            Config = new Configuration();
            this.connectionString = connectionString;
        }

        public ITableAdapter Area(string name = "Content")
        {
            return new SqlServerTableAdapter(this, name);
        }

        internal SqlConnection Connection()
        {
            return new SqlConnection(connectionString);
        }
    }
}