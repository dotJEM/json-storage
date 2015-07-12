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

        public IEnumerable<IDataMigrator> Path(string version)
        {
            //TODO: We make a temp DataMigratorEntry just for searching here, but that could probably be optimized.
            //TODO: Depends on if we are talking target or sorce version.
            int c = entries.BinarySearch(new DataMigratorEntry("", version, null), comparer);
            if (c >= 0)
            {
                return entries.Skip(c).Select(e => e.Migrator).ToList();
            }
            return entries.Skip(~c).Select(e => e.Migrator).ToList();
        }
    }
}