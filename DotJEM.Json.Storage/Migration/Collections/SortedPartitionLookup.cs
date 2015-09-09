using System.Collections.Generic;
using System.Linq;

namespace DotJEM.Json.Storage.Migration.Collections
{
    public class SortedPartitionLookup : ISortedPartitionLookup
    {
        private readonly IComparer<DataMigratorEntry> comparer;
        private readonly List<DataMigratorEntry> entries = new List<DataMigratorEntry>();

        public int Count
        {
            get { return entries.Count; }
        }

        public SortedPartitionLookup(IComparer<DataMigratorEntry> comparer)
        {
            this.comparer = comparer;
        }

        public void Add(DataMigratorEntry entry)
        {
            if (Count == 0)
            {
                entries.Add(entry);
                return;
            }
            int c = entries.BinarySearch(entry, comparer);
            if (c >= 0)
            {
                entries.Insert(c + 1, entry);
            }
            else
            {
                entries.Insert(~c, entry);
            }
        }

        public IEnumerable<IDataMigrator> MigrationPath(string version)
        {
            //TODO: We make a temp DataMigratorEntry just for searching here, but that could probably be optimized.
            var searchEntry = new DataMigratorEntry("", version, null);
            int c = entries.BinarySearch(searchEntry, comparer);
            int skip = c >= 0 ? c : ~c;

            return entries
                .Skip(skip)
                .SkipWhile(e => comparer.Compare(e, searchEntry) == 0)
                .Select(e => e.Migrator)
                .ToList();

        }
    }
}