using System.Collections;
using System.Collections.Generic;

namespace DotJEM.Json.Storage.Adapter.Materialize.Log
{
    public class ArrayPartition
    {
        public static ArrayPartition<T> Create<T>(T[] source, int start, int count) => new ArrayPartition<T>(source, start, count);

    }
    public class ArrayPartition<T> : IEnumerable<T>
    {
        private readonly int start;
        private readonly int count;
        private readonly T[] source;

        public ArrayPartition(T[] source, int start, int count)
        {
            this.start = start;
            this.count = count;
            this.source = source;
        }

        public IEnumerator<T> GetEnumerator()
        {
            int max = count + start;
            for (int i = start; i < max; i++)
                yield return source[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

}