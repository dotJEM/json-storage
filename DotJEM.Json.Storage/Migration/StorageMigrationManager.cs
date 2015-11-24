using System.Collections.Generic;
using System.Linq;
using DotJEM.Json.Storage.Configuration;
using DotJEM.Json.Storage.Migration.Collections;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Migration
{

    public interface IStorageMigrationManager
    {
        void Add(IDataMigrator migrator);

        bool Upgrade(ref JObject json);

        bool Downgrade(ref JObject json, string targetVersion);
    }

    public class StorageMigrationManager : IStorageMigrationManager
    {
        private readonly StorageConfiguration configuration;

        internal IDataMigratorInternalCollection Migrators { get; private set; }

        public StorageMigrationManager(StorageConfiguration configuration)
        {
            this.configuration = configuration;
            Migrators = new DataMigratorCollection();
        }

        // Intended for test purposes 
        public StorageMigrationManager(StorageConfiguration configuration, IEnumerable<IDataMigrator> migrators)
            : this(configuration)
        {
            foreach (var migrator in migrators)
            {
                Add(migrator);
            }
        }

        public StorageMigrationManager Initialized()
        {
            Migrators = Migrators.AsMapped(new DataMigratorComparer(configuration.VersionProvider));
            return this;
        }


        public void Add(IDataMigrator migrator)
        {
            Migrators.Add(migrator);
        }

        public bool Upgrade(ref JObject json)
        {
            string contentType = GetContentType(json);
            string version = GetVersion(json);

            IVersionProvider versionProvider = configuration.VersionProvider;
            if (versionProvider.Compare(version, versionProvider.Current) >= 0)
                return false;

            IEnumerable<IDataMigrator> path = Migrators[contentType].MigrationPath(version);
            json = path.Aggregate(json, (o, migrator) => migrator.Up(o));
            SetVersion(json, versionProvider.Current);

            return true;
        }

        public bool Downgrade(ref JObject json, string targetVersion)
        {
            string contentType = GetContentType(json);
            string version = GetVersion(json);

            IVersionProvider versionProvider = configuration.VersionProvider;
            if (versionProvider.Compare(version, targetVersion) <= 0)
                return false;

            IEnumerable<IDataMigrator> path = Migrators[contentType].MigrationPath(targetVersion).Reverse();
            json = path.Aggregate(json, (o, migrator) => Downgrade(migrator, o, version));
            SetVersion(json, targetVersion);

            return true;
        }

        private JObject Downgrade(IDataMigrator migrator, JObject entity, string entityVersion)
        {
            DataMigratorAttribute meta = DataMigratorAttribute.GetAttribute(migrator.GetType());
            string migratorVersion = meta.Version;

            IVersionProvider versionProvider = configuration.VersionProvider;
            if (versionProvider.Compare(entityVersion, migratorVersion) >= 0)
            {
                return migrator.Down(entity);
            }
            else
            {
                // Do not apply downgrade as entity has not been upgraded to migrator target version (or newer)
                return entity;
            }
        }

        private string GetContentType(JObject json)
        {
            return (string)json[configuration.Fields[JsonField.ContentType]];
        }

        private string GetVersion(JObject json)
        {
            return (string)json[configuration.Fields[JsonField.SchemaVersion]];
        }

        private void SetVersion(JObject json, string version)
        {
            json[configuration.Fields[JsonField.SchemaVersion]] = version;
        }
    }
}
