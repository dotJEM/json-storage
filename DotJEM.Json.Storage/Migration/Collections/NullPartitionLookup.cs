using System.Collections.Generic;
using System.Linq;

namespace DotJEM.Json.Storage.Migration.Collections
{
    public class NullPartitionLookup : ISortedPartitionLookup
    {
        public void Add(DataMigratorEntry entry)
        {
        }

        public IEnumerable<IDataMigrator> MigrationPath(string version)
        {
            return Enumerable.Empty<IDataMigrator>();
        }
    }
}