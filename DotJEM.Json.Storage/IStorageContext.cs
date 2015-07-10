using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using DotJEM.Json.Storage.Adapter;
using DotJEM.Json.Storage.Configuration;
using DotJEM.Json.Storage.Migration;

namespace DotJEM.Json.Storage
{
    public interface IStorageContext
    {
        IStorageConfigurator Configure { get; }
        IStorageArea Area(string name = "content");
        bool Release(string name = "content");
        IDataMigrator[] Migrators { get; set; }
    }

    public class SqlServerStorageContext : IStorageContext
    {
        private readonly string connectionString;
        private readonly Dictionary<string, IStorageArea> openAreas = new Dictionary<string, IStorageArea>();

        private IDataMigrator[] migrators;
        private IDataMigrator[] sortedMigrators;

        public IStorageConfigurator Configure { get { return Configuration; } }
        public IBsonSerializer Serializer { get; private set; }
        
        internal StorageConfiguration Configuration { get; private set; }

        public SqlServerStorageContext(string connectionString) : this(connectionString, new IDataMigrator[0])
        {
        }

        public SqlServerStorageContext(string connectionString, IDataMigrator[] migrators)
        {
            Serializer = new BsonSerializer();
            Configuration = new StorageConfiguration();

            this.connectionString = connectionString;
            this.migrators = migrators;
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

        public IDataMigrator[] Migrators
        {
            get { return sortedMigrators ?? (sortedMigrators = CreateSortedArray(migrators)); }
            set 
            {
                migrators = value;
                sortedMigrators = null;
            }
        }

        private IDataMigrator[] CreateSortedArray(IDataMigrator[] dataMigrators)
        {
            if (dataMigrators == null || dataMigrators.Length == 0)
            {
                return new IDataMigrator[0];
            }

            IDataMigrator[] sortedArray = (IDataMigrator[])dataMigrators.Clone();
            Array.Sort(sortedArray, new DataMigratorComparer(Configuration.VersionProvider));
            return sortedArray;
        }



        internal SqlConnection Connection()
        {
            return new SqlConnection(connectionString);
        }
    }

    internal class DataMigratorComparer : IComparer<IDataMigrator>
    {
        private readonly IVersionProvider versionProvider;

        internal DataMigratorComparer(IVersionProvider versionProvider)
        {
            this.versionProvider = versionProvider;
        }

        public int Compare(IDataMigrator x, IDataMigrator y)
        {
            return versionProvider.Compare(x.Version(), y.Version());
        }
    }
}