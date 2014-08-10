using System.Data.SqlClient;
using DotJEM.Json.Storage.Configuration;

namespace DotJEM.Json.Storage
{
    public interface IStorageContext
    {
        IStorageConfigurator Configure { get; }
        IStorageArea Area(string name = "content");
    }

    public class SqlServerStorageContext : IStorageContext
    {
        private readonly string connectionString;

        public IStorageConfigurator Configure { get { return Configuration; } }
        public IBsonSerializer Serializer { get; private set; }
        
        internal StorageConfiguration Configuration { get; private set; }

        public SqlServerStorageContext(string connectionString)
        {
            Serializer = new BsonSerializer();
            Configuration = new StorageConfiguration();

            this.connectionString = connectionString;
        }

        public IStorageArea Area(string name = "content")
        {
            //TODO: Cache configurations?
            return new SqlServerStorageArea(this, name);
        }

        internal SqlConnection Connection()
        {
            return new SqlConnection(connectionString);
        }
    }
}