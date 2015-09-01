using System.Collections.Generic;
using System.Linq;
using DotJEM.Json.Storage.Configuration;
using DotJEM.Json.Storage.Migration.Collections;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Migration
{
    public class StorageMigrationManager
    {
        private readonly StorageConfiguration configuration;

        internal IDataMigratorInternalCollection Migrators { get; private set; }

        public StorageMigrationManager(StorageConfiguration configuration)
        {
            this.configuration = configuration;
            Migrators = new DataMigratorCollection();
        }

        public StorageMigrationManager Initialized()
        {
            Migrators = Migrators.AsMapped(new DataMigratorComparer(configuration.VersionProvider));
            return this;
        }


        public bool Migrate(ref JObject json)
        {
            string contentType = (string)json[configuration.Fields[JsonField.ContentType]];
            string version = (string)json[configuration.Fields[JsonField.SchemaVersion]];

            IVersionProvider versionProvider = configuration.VersionProvider;
            if (versionProvider.Compare(version, versionProvider.Current) == 0)
                return false;

            IEnumerable<IDataMigrator> path = Migrators[contentType].MigrationPath(version);
            json = path.Aggregate(json, (o, migrator) => migrator.Migrate(o));
            json[configuration.Fields[JsonField.SchemaVersion]] = versionProvider.Current;

            return true;
        }

    }
}
