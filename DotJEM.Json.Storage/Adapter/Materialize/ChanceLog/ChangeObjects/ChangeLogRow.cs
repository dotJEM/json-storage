using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Adapter.Materialize.ChanceLog.ChangeObjects
{
    
    public abstract class ChangeLogRow : IChangeLogRow
    {
        protected IStorageContext Context { get; private set; }

        public abstract int Size { get; }

        public string Area { get; }
        public long Generation { get; }
        public Guid Id { get; }

        public abstract ChangeType Type { get; }

        protected ChangeLogRow(IStorageContext context, string area, long token, Guid id)
        {
            Area = area;
            Generation = token;
            Id = id;
            Context = context;
        }

        public abstract JObject CreateEntity();

        public abstract JsonReader OpenReader();

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Context = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}