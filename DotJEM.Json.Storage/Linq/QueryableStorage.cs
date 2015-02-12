using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Linq
{
    public class JObjectMetaEntity : DynamicMetaObject
    {
        public JObjectMetaEntity(Expression expression, BindingRestrictions restrictions) : base(expression, restrictions)
        {
        }

        public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
        {
            return base.BindGetMember(binder);
        }

        public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
        {
            return base.BindSetMember(binder, value);
        }
    }

    //http://blogs.msdn.com/b/mattwar/archive/2008/11/18/linq-links.aspx
    public class JObjectEntity : DynamicObject
    {
        //Note Reserved fields
        public Guid Id { get; private set; }
        public string Reference { get; private set; }
        public string ContentType { get; private set; }
        public int Version { get; private set; }

        public DateTime Created { get; private set; }
        public DateTime Updated { get; private set; }

        public JObject Entity { get; set; }

        public JObjectEntity()
        {
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            // Huh?? DynamicProxyMetaObject<T>
            result = Entity[binder.Name];
            return true;
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            return base.TrySetMember(binder, value);
        }
    }

    public class StorageAreaQ
    {
        private DbConnection connection;

        public Query<JObjectEntity> Open(string area)
        {
            return new Query<JObjectEntity>(new StorageAreaQueryProvider(connection, area));
        }

        public StorageAreaQ(DbConnection connection)
        {
            this.connection = connection;
        }
    }



    public class StorageAreaQueryProvider : QueryProvider
    {
        readonly DbConnection connection;
        private readonly string area;

        public StorageAreaQueryProvider(DbConnection connection, string area)
        {
            this.connection = connection;
            this.area = area;
        }

        public override string GetQueryText(Expression expression)
        {
            return Translate(expression);
        }

        public override object Execute(Expression expression)
        {
            DbCommand cmd = connection.CreateCommand();
            cmd.CommandText = Translate(expression);
            DbDataReader reader = cmd.ExecuteReader();
            Type elementType = TypeSystem.GetElementType(expression.Type);
            return Activator.CreateInstance(
                typeof(ObjectReader<>).MakeGenericType(elementType),
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { reader },
                null);
        }

        private string Translate(Expression expression)
        {
            return new QueryTranslator(area).Translate(expression);
        }
    }


    public class StorageAreaContext : IOrderedQueryable<JObjectEntity>
    {
        public StorageAreaContext(string area)
        {
            Provider = new StorageAreaQueryableProvider(area);
            Expression = Expression.Constant(this);
        }

        public IEnumerator<JObjectEntity> GetEnumerator()
        {
            return Provider.Execute<IEnumerable<JObjectEntity>>(Expression).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public Type ElementType
        {
            get { return typeof(JObjectEntity); }
        }

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
            Debug.WriteLine(expression);


            return new List<TElement>().AsQueryable();
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

    public class Query<T> : IQueryable<T>, IQueryable, IEnumerable<T>, IEnumerable, IOrderedQueryable<T>, IOrderedQueryable
    {
        private readonly QueryProvider provider;
        private readonly Expression expression;

        public Query(QueryProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException("provider");
            }
            this.provider = provider;
            expression = Expression.Constant(this);
        }

        public Query(QueryProvider provider, Expression expression)
        {
            if (provider == null)
            {
                throw new ArgumentNullException("provider");
            }
            if (expression == null)
            {
                throw new ArgumentNullException("expression");
            }
            if (!typeof(IQueryable<T>).IsAssignableFrom(expression.Type))
            {
                throw new ArgumentOutOfRangeException("expression");
            }
            this.provider = provider;
            this.expression = expression;
        }

        Expression IQueryable.Expression
        {
            get { return expression; }
        }

        Type IQueryable.ElementType
        {
            get { return typeof(T); }
        }

        IQueryProvider IQueryable.Provider
        {
            get { return provider; }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return ((IEnumerable<T>)provider.Execute(expression)).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)provider.Execute(expression)).GetEnumerator();
        }

    }

    public abstract class QueryProvider : IQueryProvider
    {
        protected QueryProvider()
        {
        }

        IQueryable<S> IQueryProvider.CreateQuery<S>(Expression expression)
        {
            return new Query<S>(this, expression);
        }

        IQueryable IQueryProvider.CreateQuery(Expression expression)
        {
            Type elementType = TypeSystem.GetElementType(expression.Type);
            try
            {
                return (IQueryable)Activator.CreateInstance(typeof(Query<>).MakeGenericType(elementType), new object[] { this, expression });
            }
            catch (TargetInvocationException tie)
            {
                throw tie.InnerException;
            }
        }

        S IQueryProvider.Execute<S>(Expression expression)
        {
            return (S)Execute(expression);
        }

        object IQueryProvider.Execute(Expression expression)
        {
            return Execute(expression);
        }

        public abstract string GetQueryText(Expression expression);
        public abstract object Execute(Expression expression);
    }

    internal class ObjectReader<T> : IEnumerable<T>, IEnumerable where T : class, new()
    {
        Enumerator enumerator;

        internal ObjectReader(DbDataReader reader)
        {
            enumerator = new Enumerator(reader);
        }

        public IEnumerator<T> GetEnumerator()
        {
            Enumerator e = enumerator;
            if (e == null)
            {
                throw new InvalidOperationException("Cannot enumerate more than once");
            }
            enumerator = null;
            return e;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        class Enumerator : IEnumerator<T>, IEnumerator, IDisposable
        {
            DbDataReader reader;
            FieldInfo[] fields;
            int[] fieldLookup;
            T current;

            internal Enumerator(DbDataReader reader)
            {
                this.reader = reader;
                fields = typeof(T).GetFields();
            }

            public T Current
            {
                get { return current; }
            }

            object IEnumerator.Current
            {
                get { return current; }
            }

            public bool MoveNext()
            {
                if (reader.Read())
                {
                    if (fieldLookup == null)
                    {
                        InitFieldLookup();
                    }
                    T instance = new T();
                    for (int i = 0, n = fields.Length; i < n; i++)
                    {
                        int index = fieldLookup[i];
                        if (index >= 0)
                        {
                            FieldInfo fi = fields[i];
                            if (reader.IsDBNull(index))
                            {
                                fi.SetValue(instance, null);
                            }
                            else
                            {
                                fi.SetValue(instance, reader.GetValue(index));
                            }
                        }
                    }
                    current = instance;
                    return true;
                }
                return false;
            }

            public void Reset()
            {
            }

            public void Dispose()
            {
                reader.Dispose();
            }

            private void InitFieldLookup()
            {
                Dictionary<string, int> map = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
                for (int i = 0, n = reader.FieldCount; i < n; i++)
                {
                    map.Add(reader.GetName(i), i);
                }
                fieldLookup = new int[fields.Length];
                for (int i = 0, n = fields.Length; i < n; i++)
                {
                    int index;
                    if (map.TryGetValue(fields[i].Name, out index))
                    {
                        fieldLookup[i] = index;
                    }
                    else
                    {
                        fieldLookup[i] = -1;
                    }
                }
            }
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

    internal static class TypeSystem
    {
        internal static Type GetElementType(Type seqType)
        {
            Type ienum = FindIEnumerable(seqType);
            if (ienum == null) return seqType;
            return ienum.GetGenericArguments()[0];
        }

        private static Type FindIEnumerable(Type seqType)
        {
            if (seqType == null || seqType == typeof(string))
                return null;
            if (seqType.IsArray)
                return typeof(IEnumerable<>).MakeGenericType(seqType.GetElementType());
            if (seqType.IsGenericType)
            {
                foreach (Type arg in seqType.GetGenericArguments())
                {
                    Type ienum = typeof(IEnumerable<>).MakeGenericType(arg);
                    if (ienum.IsAssignableFrom(seqType))
                    {
                        return ienum;
                    }
                }
            }
            Type[] ifaces = seqType.GetInterfaces();
            if (ifaces != null && ifaces.Length > 0)
            {
                foreach (Type iface in ifaces)
                {
                    Type ienum = FindIEnumerable(iface);
                    if (ienum != null) return ienum;
                }
            }
            if (seqType.BaseType != null && seqType.BaseType != typeof(object))
            {
                return FindIEnumerable(seqType.BaseType);
            }
            return null;
        }
    }
}
