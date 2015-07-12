using System;
using System.Collections.Generic;

namespace DotJEM.Json.Storage.Migration.Collections
{
    internal static class DataMigratorCollectionExtensions
    {
        public static MappedDataMigratorCollection AsMapped(this IDataMigratorCollection self, IComparer<DataMigratorEntry> comparer)
        {
            MappedDataMigratorCollection premapped = self as MappedDataMigratorCollection;
            if (premapped != null)
                return premapped;

            DataMigratorCollection notmapped = self as DataMigratorCollection;
            if (notmapped != null)
                return new MappedDataMigratorCollection(notmapped.Migrators, comparer);

            throw new InvalidOperationException("Could not convert " + self.GetType() + " to MappedDataMigratorCollection");
        }
    }
}