using System.Collections.Generic;

namespace DotJEM.Json.Storage.Migration.Collections
{
    internal interface IDataMigratorInternalCollection : IDataMigratorCollection
    {
        MappedDataMigratorCollection AsMapped(IComparer<DataMigratorEntry> comparer);

        ISortedPartitionLookup this[string contentType] { get; }
    }
}
