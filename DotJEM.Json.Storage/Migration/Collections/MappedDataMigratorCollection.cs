using System;
using System.Collections.Generic;
using System.Linq;

namespace DotJEM.Json.Storage.Migration.Collections
{
    public class MappedDataMigratorCollection : IDataMigratorCollection
    {
        private readonly IComparer<DataMigratorEntry> comparer;
        private readonly Dictionary<string, ISortedPartitionLookup> map = new Dictionary<string, ISortedPartitionLookup>();

        public MappedDataMigratorCollection(List<IDataMigrator> migrators, IComparer<DataMigratorEntry> comparer)
        {
            this.comparer = comparer;
            migrators.ForEach(m => Add(m));
        }

        public IDataMigratorCollection Add(IDataMigrator instance)
        {
            DataMigratorAttribute meta = DataMigratorAttribute.GetAttribute(instance.GetType());
            if (meta == null)
                throw new InvalidOperationException("IDataMigrator implementation must define a DataMigratorAttribute.");

            ISortedPartitionLookup sortedPartition;
            if (!map.TryGetValue(meta.ContentType, out sortedPartition))
                map.Add(meta.ContentType, sortedPartition = new SortedPartitionLookup(comparer));

            DataMigratorEntry entry = new DataMigratorEntry(meta.ContentType, meta.Version, instance);
            sortedPartition.Add(entry);
            return this;
        }

        public IEnumerable<IDataMigrator> MigrationPath(string contentType, string version)
        {
            ISortedPartitionLookup sortedPartition;
            return !map.TryGetValue(contentType, out sortedPartition)
                ? Enumerable.Empty<IDataMigrator>()
                : sortedPartition.Path(version);
        }
    }
}