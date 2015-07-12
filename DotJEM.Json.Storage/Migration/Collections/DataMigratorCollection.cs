using System;
using System.Collections.Generic;

namespace DotJEM.Json.Storage.Migration.Collections
{
    public class DataMigratorCollection : IDataMigratorCollection
    {
        private readonly List<IDataMigrator> migrators = new List<IDataMigrator>();

        public List<IDataMigrator> Migrators
        {
            get { return migrators; }
        }

        public IEnumerable<IDataMigrator> MigrationPath(string contenttype, string version)
        {
            throw new NotSupportedException("MigrationPath lookups are only supported in initialized mode.");
        }

        public IDataMigratorCollection Add(IDataMigrator instance)
        {
            migrators.Add(instance);
            return this;
        }
    }
}