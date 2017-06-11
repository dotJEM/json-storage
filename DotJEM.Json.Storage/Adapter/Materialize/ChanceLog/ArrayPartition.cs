using System.Collections;
using System.Collections.Generic;

namespace DotJEM.Json.Storage.Adapter.Materialize.Log
{
    public class ArrayPartition
    {
        public static ArrayPartition<T> Create<T>(T[] source, int start, int stop) => new ArrayPartition<T>(source, start, stop);

    }
    public class ArrayPartition<T> : IEnumerable<T>
    {
        private readonly int start;
        private readonly int stop;
        private readonly T[] source;

        public ArrayPartition(T[] source, int start, int stop)
        {
            this.start = start;
            this.stop = stop;
            this.source = source;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = start; i < stop; i++)
                yield return source[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

}