using System.Collections.Generic;
using System.Data.SqlClient;
using DotJEM.Json.Storage.Configuration;

namespace DotJEM.Json.Storage
{
    public interface IStorageContext
    {
        IStorageConfigurator Configure { get; }
        IStorageArea Area(string name = "content");
        bool Release(string name = "content");
    }

    public class SqlServerStorageContext : IStorageContext
    {
        private readonly string connectionString;
        private readonly Dictionary<string, IStorageArea> openAreas = new Dictionary<string, IStorageArea>(); 

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
            if (!openAreas.ContainsKey(name))
                return openAreas[name] = new SqlServerStorageArea(this, name);
            return openAreas[name];
        }

        public bool Release(string name = "content")
        {
            return openAreas.Remove(name);
        }

        internal SqlConnection Connection()
        {
            return new SqlConnection(connectionString);
        }
    }
}