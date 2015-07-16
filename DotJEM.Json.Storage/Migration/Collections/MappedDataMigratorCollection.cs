using System;
using System.Collections.Generic;
using System.Linq;

namespace DotJEM.Json.Storage.Migration.Collections
{
    public class MappedDataMigratorCollection : IDataMigratorInternalCollection
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

        public MappedDataMigratorCollection AsMapped(IComparer<DataMigratorEntry> comparer)
        {
            return this;
        }

        public ISortedPartitionLookup this[string contentType]
        {
            get
            {
                ISortedPartitionLookup sortedPartition;
                return !map.TryGetValue(contentType, out sortedPartition)
                    ? new NullPartitionLookup()
                    : sortedPartition;
            }
        }

    }
}