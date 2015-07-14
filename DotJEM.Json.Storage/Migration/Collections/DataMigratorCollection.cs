using System;
using System.Collections.Generic;

namespace DotJEM.Json.Storage.Migration.Collections
{
    public class DataMigratorCollection : IDataMigratorInternalCollection
    {
        private readonly List<IDataMigrator> migrators = new List<IDataMigrator>();

        public List<IDataMigrator> Migrators
        {
            get { return migrators; }
        }

        public IDataMigratorCollection Add(IDataMigrator instance)
        {
            migrators.Add(instance);
            return this;
        }

        public MappedDataMigratorCollection AsMapped(IComparer<DataMigratorEntry> comparer)
        {
            return new MappedDataMigratorCollection(Migrators, comparer);
        }

        public ISortedPartitionLookup this[string contentType]
        {
            get
            {
                throw new NotSupportedException("Content type lookups are only supported in initialized mode.");
            }
        }
    }
}