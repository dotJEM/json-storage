using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DotJEM.Json.Storage.Adapter.Materialize.ChanceLog.ChangeObjects;
using DotJEM.Json.Storage.Adapter.Materialize.Log;

namespace DotJEM.Json.Storage.Adapter.Materialize.ChanceLog
{
    /// <summary>
    /// Provides a set of changes occured within a <see cref="IStorageArea"/>, this can be usefull to use for notifications etc.
    /// </summary>
    public interface IStorageChangeCollection : IEnumerable<IChangeLogRow>
    {
        /// <summary>
        /// Gets the name of the <see cref="IStorageArea"/> from where the changes came.
        /// </summary>
        string StorageArea { get; }

        /// <summary>
        /// Gets the last token in the collection, this can be used to acquire the next batch.
        /// This is also the same token that is automatically used if no token is provided.
        /// </summary>
        long Generation { get; }

        /// <summary>
        /// Gets the number of changes in the current change collection, the count object
        /// contains counts for Created, Updated and Deleted individually as well as totals.
        /// <see cref="ChangeCount"/> for more details.
        /// </summary>
        ChangeCount Count { get; }

        /// <summary>
        /// Gets all changes in a Partitioned <see cref="IEnumerable{T}"/> with Created first followed
        /// by Updates and finally Deletes, within each type the order is preserved.
        /// </summary>
        IEnumerable<IChangeLogRow> Partitioned { get; }

        /// <summary>
        /// Gets all changes that was creation of objects.
        /// </summary>
        IEnumerable<IChangeLogRow> Created { get; }

        /// <summary>
        /// Gets all changes that was updates of objects.
        /// </summary>
        IEnumerable<IChangeLogRow> Updated { get; }
        
        /// <summary>
        /// Gets all changes that was deletions of objects.
        /// </summary>
        IEnumerable<IChangeLogRow> Deleted { get; }

        /// Gets all changes that was in a faulty state.
        /// </summary>
        IEnumerable<IChangeLogRow> Faulted { get; }
    }

    public class StorageChangeCollection : IStorageChangeCollection
    {
        private readonly List<IChangeLogRow> changes;
        private readonly IChangeLogRow[] partitioned;

        public IEnumerable<IChangeLogRow> Partitioned => new ReadOnlyCollection<IChangeLogRow>(partitioned);

        public string StorageArea { get; }
        public long Generation { get; }

        public ChangeCount Count { get; }
        public IEnumerable<IChangeLogRow> Created { get; }
        public IEnumerable<IChangeLogRow> Updated { get; }
        public IEnumerable<IChangeLogRow> Deleted { get; }
        public IEnumerable<IChangeLogRow> Faulted { get; }

        public StorageChangeCollection(string storageArea, long generation, List<IChangeLogRow> changes)
        {
            StorageArea = storageArea;
            Generation = generation;
            this.changes = changes;

            //Note: This is basically sorting the changes based on type, but there is a few things to note here:
            //      A) this is strictly a 2*N sorting algorithm (generally faster above 4 - 4 items have the same O) rather than a N Log (N).
            //         We can do this as we know there to me a max of 3 destict values so all we have to do is count them and
            //         then perform a liniar insert based on those counts.
            //
            //      B) We preserve the order of changes as they apear if we did a "filter" on the original list instead.
            int[] count = new int[4];
            count[(int)ChangeType.Create] = 0;
            count[(int)ChangeType.Update] = 0;
            count[(int)ChangeType.Delete] = 0;
            count[(int)ChangeType.Faulty] = 0;
            foreach (IChangeLogRow change in changes)
                count[(int) change.Type]++;

            Count = new ChangeCount(count[(int)ChangeType.Create], count[(int)ChangeType.Update], count[(int)ChangeType.Delete], count[(int)ChangeType.Faulty]);

            partitioned = new IChangeLogRow[changes.Count];

            int[] cursor = new int[4];
            cursor[(int)ChangeType.Create] = 0;
            cursor[(int)ChangeType.Update] = count[(int)ChangeType.Create];
            cursor[(int)ChangeType.Delete] = count[(int)ChangeType.Create] + count[(int)ChangeType.Update];
            cursor[(int)ChangeType.Faulty] = count[(int)ChangeType.Create] + count[(int)ChangeType.Update] + count[(int)ChangeType.Faulty];
            foreach (IChangeLogRow change in changes)
            {
                int i = cursor[(int) change.Type]++;
                partitioned[i] = change;
            }

            Created = ArrayPartition.Create(partitioned, 0, count[(int)ChangeType.Create]);
            Updated = ArrayPartition.Create(partitioned, cursor[(int) ChangeType.Create], count[(int) ChangeType.Update]);
            Deleted = ArrayPartition.Create(partitioned, cursor[(int)ChangeType.Update], count[(int)ChangeType.Delete]);
            Faulted = ArrayPartition.Create(partitioned, cursor[(int)ChangeType.Delete], count[(int)ChangeType.Faulty]);
        }

        public IEnumerator<IChangeLogRow> GetEnumerator() => changes.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString()
        {
            //return $"Created: {Created}, Updated: {Updated}, Deleted: {Deleted}";
            return $"[GEN:{Generation}] {Count} from {StorageArea}";
        }
    }


}