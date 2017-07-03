using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DotJEM.Json.Storage.Adapter.Materialize.Log;

namespace DotJEM.Json.Storage.Adapter.Materialize.ChanceLog
{
    /// <summary>
    /// Provides a set of changes occured within a <see cref="IStorageArea"/>, this can be usefull to use for notifications etc.
    /// </summary>
    public interface IStorageChangeCollection : IEnumerable<Change>
    {
        /// <summary>
        /// Gets the name of the <see cref="IStorageArea"/> from where the changes came.
        /// </summary>
        string StorageArea { get; }

        /// <summary>
        /// Gets the last token in the collection, this can be used to acquire the next batch.
        /// This is also the same token that is automatically used if no token is provided.
        /// </summary>
        long Token { get; }

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
        IEnumerable<Change> Partitioned { get; }

        /// <summary>
        /// Gets all changes that was creation of objects.
        /// </summary>
        IEnumerable<Change> Created { get; }

        /// <summary>
        /// Gets all changes that was updates of objects.
        /// </summary>
        IEnumerable<Change> Updated { get; }
        
        /// <summary>
        /// Gets all changes that was deletions of objects.
        /// </summary>
        IEnumerable<Change> Deleted { get; }


    }

    public class StorageChangeCollection : IStorageChangeCollection
    {
        private readonly List<Change> changes;
        private readonly Change[] partitions;
        public IEnumerable<Change> Partitioned => new ReadOnlyCollection<Change>(partitions);
        public string StorageArea { get; }
        public long Token { get; }
        public ChangeCount Count { get; }
        public IEnumerable<Change> Created { get; }
        public IEnumerable<Change> Updated { get; }
        public IEnumerable<Change> Deleted { get; }

        public StorageChangeCollection(string storageArea, long token, List<Change> changes)
        {
            StorageArea = storageArea;
            Token = token;
            this.changes = changes;

            //Note: This is basically sorting the changes based on type, but there is a few things to note here:
            //      A) this is strictly a 2*N sorting algorithm (generally faster) rather than a N Log (N).
            //         We can do this as we know there to me a max of 3 destict values so all we have to do is count them and
            //         then perform a liniar insert based on those counts.
            //
            //      B) We preserve the order of changes as they apear if we did a "filter" on the original list instead.
            int[] count = new int[3];
            count[(int)ChangeType.Create] = 0;
            count[(int)ChangeType.Update] = 0;
            count[(int)ChangeType.Delete] = 0;
            foreach (Change change in changes)
                count[(int) change.Type]++;

            Count = new ChangeCount(count[(int)ChangeType.Create], count[(int)ChangeType.Update], count[(int)ChangeType.Delete]);

            partitions = new Change[count.Sum()];

            int[] cursor = new int[3];
            cursor[(int)ChangeType.Create] = 0;
            cursor[(int)ChangeType.Update] = count[(int)ChangeType.Create];
            cursor[(int)ChangeType.Delete] = count[(int)ChangeType.Create]+count[(int)ChangeType.Update];
            foreach (Change change in changes)
            {
                int i = cursor[(int) change.Type]++;
                partitions[i] = change;
            }

            Created = ArrayPartition.Create(partitions, 0, count[(int)ChangeType.Create]);
            Updated = ArrayPartition.Create(partitions, cursor[(int) ChangeType.Create], count[(int) ChangeType.Update]);
            Deleted = ArrayPartition.Create(partitions, cursor[(int)ChangeType.Update], count[(int)ChangeType.Delete]);


        }

        public IEnumerator<Change> GetEnumerator() => changes.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }


}