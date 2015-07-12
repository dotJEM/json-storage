using System;
using System.Linq;
using DotJEM.Json.Storage.Configuration;
using DotJEM.Json.Storage.Migration.Collections;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Migration
{
    public class StorageMigrationManager
    {
        private readonly string schemaVersionField;
        private readonly IVersionProvider versionProvider;
        private readonly IDataMigratorCollection migrators;

        public StorageMigrationManager(IDataMigratorCollection migrators, IVersionProvider versionProvider, string schemaVersionField)
        {
            this.migrators = migrators;
            this.versionProvider = versionProvider;
            this.schemaVersionField = schemaVersionField;
        }

        public bool Migrate(ref JObject json)
        {
            string version = (string)json[schemaVersionField];

            if (versionProvider.Compare(version, versionProvider.Current) == 0)
                return false;

            var path = migrators.MigrationPath("contentType", version);
            json = path.Aggregate(json, (o, migrator) => migrator.Migrate(o));
            json[schemaVersionField] = versionProvider.Current;
            //var dataMigrators = context.Migrators;

            //var objectSchemaVersion = json[context.Configuration.Fields[JsonField.SchemaVersion]];
            //var startMigrationIndex = GetMigrationStartIndex(dataMigrators,
            //    objectSchemaVersion != null ? objectSchemaVersion.ToString() : "");

            //if (startMigrationIndex < dataMigrators.Length)
            //{
            //    // Migrate
            //    var migratedJson = json;
            //    for (var i = startMigrationIndex; i < dataMigrators.Length; i++)
            //    {
            //        migratedJson = dataMigrators[i].Migrate(migratedJson);
            //    }
            //    migratedJson[context.Configuration.Fields[JsonField.SchemaVersion]] =
            //        context.Configuration.VersionProvider.Current;

            //    var guid = new Guid(json[context.Configuration.Fields[JsonField.Id]].ToString());
            //    Update(guid, migratedJson);

            //    return migratedJson;
            //}
            //// Up-to-date
            return true;
        }

        private int GetMigrationStartIndex(IDataMigrator[] dataMigrators, string objectSchemaVersion)
        {
            //int i;
            //for (i = dataMigrators.Length - 1; i >= 0; i--)
            //{
            //    if (context.Configuration.VersionProvider.Compare(dataMigrators[i].Version(), objectSchemaVersion) <= 0)
            //    {
            //        break;
            //    }
            //}
            //return i + 1;
            return 0;
        }

        public JObject Migrate(JObject json, Func<object, JObject> func)
        {
            throw new NotImplementedException();
        }
    }
}