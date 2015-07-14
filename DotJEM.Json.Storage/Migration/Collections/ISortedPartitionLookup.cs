using System.Collections.Generic;

namespace DotJEM.Json.Storage.Migration.Collections
{
    public interface ISortedPartitionLookup
    {
        void Add(DataMigratorEntry entry);
        IEnumerable<IDataMigrator> MigrationPath(string version);
    }
}