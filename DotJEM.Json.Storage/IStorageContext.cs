using System.Data.SqlClient;
using DotJEM.Json.Storage.Configuration;

namespace DotJEM.Json.Storage
{
    public interface IStorageContext
    {
        IStorageConfiguration Configuration { get; }
        IStorageArea Area(string name = "Content");
    }

    public class SqlServerStorageContext : IStorageContext
    {
        private readonly string connectionString;

        public IStorageConfiguration Configuration { get; private set; }
        public IBsonSerializer Serializer { get; private set; }


        public SqlServerStorageContext(string connectionString)
        {
            Serializer = new BsonSerializer();
            Configuration = new StorageConfiguration();

            this.connectionString = connectionString;
        }

        public IStorageArea Area(string name = "Content")
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