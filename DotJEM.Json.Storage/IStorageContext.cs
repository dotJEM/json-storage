using System.Data.SqlClient;
using DotJEM.Json.Storage.Configuration;

namespace DotJEM.Json.Storage
{
    public interface IStorageContext
    {
        IConfiguration Config { get; }
        IStorageArea Area(string name = "Content");
    }

    public class SqlServerStorageContext : IStorageContext
    {
        private readonly string connectionString;

        public IConfiguration Config { get; private set; }

        public SqlServerStorageContext(string connectionString)
        {
            Config = new Configuration.Configuration();
            this.connectionString = connectionString;
        }

        public IStorageArea Area(string name = "Content")
        {
            return new SqlServerStorageArea(this, name);
        }

        internal SqlConnection Connection()
        {
            return new SqlConnection(connectionString);
        }
    }
}