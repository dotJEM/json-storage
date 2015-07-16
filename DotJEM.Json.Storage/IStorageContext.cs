using System.Collections.Generic;
using System.Data.SqlClient;
using DotJEM.Json.Storage.Adapter;
using DotJEM.Json.Storage.Configuration;
using DotJEM.Json.Storage.Migration;
using DotJEM.Json.Storage.Migration.Collections;

namespace DotJEM.Json.Storage
{
    public interface IStorageContext
    {
        IStorageConfigurator Configure { get; }
        IDataMigratorCollection Migrators { get; }

        IStorageArea Area(string name = "content");
        bool Release(string name = "content");
    }

    public class SqlServerStorageContext : IStorageContext
    {
        private readonly string connectionString;
        private readonly Dictionary<string, IStorageArea> openAreas = new Dictionary<string, IStorageArea>();
        private readonly StorageMigrationManager manager;

        public IBsonSerializer Serializer { get; private set; }
        public IStorageConfigurator Configure { get { return Configuration; } }

        public IDataMigratorCollection Migrators
        {
            get { return manager.Migrators; }
        }

        internal StorageConfiguration Configuration { get; private set; }

        public SqlServerStorageContext(string connectionString)
        {
            this.connectionString = connectionString;

            Serializer = new BsonSerializer();
            Configuration = new StorageConfiguration();

            manager = new StorageMigrationManager(Configuration); 
        }

        public IStorageArea Area(string name = "content")
        {
            if (!openAreas.ContainsKey(name))
                return openAreas[name] = new SqlServerStorageArea(this, name, manager.Initialized());
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