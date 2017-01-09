using System;

namespace DotJEM.Json.Storage.Stress.Index
{
    public class ChangesIndex
    {

        //public Guid this[long key]
        //{
            
        //}

        //public TValue this[TKey key]
        //{
        //    get
        //    {
        //        TValue result;
        //        if (!TryGetValue(key, out result))
        //            throw new IndexOutOfRangeException();
        //        return result;
        //    }
        //    set
        //    {
        //        InsertValue ii = new InsertValue(value, true);
        //        AddEntry(key, ref ii);
        //    }
        //}
        public Guid this[long changeIndex]
        {
            get { return Guid.Empty; }
        }

        public long Count  => 0;

        public void Add(Guid guid)
        {
            
        }

        public void Delete(Guid id)
        {
            
        }
    }
}
