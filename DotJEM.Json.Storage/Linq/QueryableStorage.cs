using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Linq
{
    public class StorageAreaContext : IOrderedQueryable<JObject>
    {
        public StorageAreaContext(string area)
        {
            Provider = new StorageAreaQueryableProvider(area);
        }

        public IEnumerator<JObject> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public Expression Expression { get; private set; }
        public Type ElementType { get; private set; }
        public IQueryProvider Provider { get; private set; }
    }

    public class StorageAreaQueryableProvider : IQueryProvider
    {
        private readonly string area;

        public StorageAreaQueryableProvider(string area)
        {
            this.area = area;
        }

        public IQueryable CreateQuery(Expression expression)
        {
            throw new NotImplementedException();
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            throw new NotImplementedException();
        }

        public object Execute(Expression expression)
        {
            throw new NotImplementedException();
        }

        public TResult Execute<TResult>(Expression expression)
        {
            throw new NotImplementedException();
        }
    }
}
