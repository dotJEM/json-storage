using System.Collections;
using System.Collections.Generic;

namespace DotJEM.Json.Storage.Adapter.Materialize.ChanceLog
{
    public class ArrayPartition
    {
        public static ArrayPartition<T> Create<T>(T[] source, int start, int count) => new ArrayPartition<T>(source, start, count);
    }

    public class ArrayPartition<T> : IEnumerable<T>
    {
        private readonly int start;
        private readonly int stop;
        private readonly T[] source;

        public ArrayPartition(T[] source, int start, int count)
        {
            this.start = start;
            this.stop = count + start;
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