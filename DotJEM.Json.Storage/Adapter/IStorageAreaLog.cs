using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Adapter
{
    public interface IStorageAreaLog
    {
        IStorageChanges Insert(JObject original, JObject changed);
        IStorageChanges Get(long token);
    }

    public interface IStorageChanges
    {
        long Token { get; }
        IEnumerable<JObject> Changes { get; }
    }

    public class SqlServerStorageAreaLog : IStorageAreaLog
    {
        public IStorageChanges Insert(JObject original, JObject changed)
        {
            throw new NotImplementedException();
        }

        public IStorageChanges Get(long token)
        {
            throw new NotImplementedException();
        }
    }
}