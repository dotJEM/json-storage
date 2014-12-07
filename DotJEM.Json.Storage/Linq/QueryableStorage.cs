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
            yield return new JObject();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public Type ElementType { get; private set; }
        public Expression Expression { get; private set; }
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

    //public class QueryProvider : IQueryProvider
    //{
    //    private readonly IQueryContext queryContext;

    //    public QueryProvider(IQueryContext queryContext)
    //    {
    //        this.queryContext = queryContext;
    //    }

    //    public virtual IQueryable CreateQuery(Expression expression)
    //    {
    //        Type elementType = TypeSystem.GetElementType(expression.Type);
    //        try
    //        {
    //            return
    //               (IQueryable)Activator.CreateInstance(typeof(Queryable<>).
    //                      MakeGenericType(elementType), new object[] { this, expression });
    //        }
    //        catch (TargetInvocationException e)
    //        {
    //            throw e.InnerException;
    //        }
    //    }

    //    public virtual IQueryable<T> CreateQuery<T>(Expression expression)
    //    {
    //        return new Queryable<T>(this, expression);
    //    }

    //    object IQueryProvider.Execute(Expression expression)
    //    {
    //        return queryContext.Execute(expression, false);
    //    }

    //    T IQueryProvider.Execute<T>(Expression expression)
    //    {
    //        return (T)queryContext.Execute(expression,
    //                   (typeof(T).Name == "IEnumerable`1"));
    //    }
    //}
}
