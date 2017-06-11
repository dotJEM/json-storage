using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Adapter.Materialize.ChanceLog
{
    public enum ChangeType
    {
        Create, Update, Delete
    }

    /// <summary>
    /// Represents a change in the storage area, changes can be of different types.
    /// </summary>
    public abstract class Change
    {
        private JObject cached;

        public long Token { get; }
        public ChangeType Type { get; }
        public Guid Id { get; }
        public JObject Entity => cached ?? (cached = CreateEntity());

        public abstract int Size { get; }

        protected Change(long token, ChangeType type, Guid id)
        {
            Token = token;
            Type = type;
            Id = id;
        }

        public abstract JObject CreateEntity();

        public abstract JsonReader OpenReader();

        //TODO: Perhaps something with WeakReference? -> WeakReference<JObject> json = new WeakReference<JObject>(null);
        public static implicit operator JObject(Change change) => change.cached ?? change.CreateEntity();

    }

    public sealed class SqlServerInsertedChange : Change
    {
        private readonly JObject changed;
        public override int Size => 0;

        public SqlServerInsertedChange(JObject changed, long token, ChangeType type, Guid id) : base(token, type, id)
        {
            this.changed = changed;
        }

        public override JObject CreateEntity() => changed;
        public override JsonReader OpenReader() => new JTokenReader(CreateEntity());
    }

    public sealed class SqlServerEntityChange : Change
    {
        private readonly Func<SqlServerEntityChange, JObject> fac;

        public string ContentType { get; }
        public string Area { get; }
        public long Reference { get; }
        public int Version { get; }
        public DateTime Created { get; }
        public DateTime Updated { get; }
        public byte[] Data { get; }

        public override int Size => Data.Length;

        public SqlServerEntityChange(Func<SqlServerEntityChange, JObject> fac, long token, ChangeType type, Guid id, string contentType, string area, long reference, int version, DateTime created, DateTime updated, byte[] data)
            : base(token, type, id)
        {
            this.fac = fac;
            ContentType = contentType;
            Area = area;
            Reference = reference;
            Version = version;
            Created = created;
            Updated = updated;
            Data = data;
        }

        public override JObject CreateEntity() => fac(this);
        public override JsonReader OpenReader() => new BsonDataReader(new MemoryStream(Data));
    }

    public sealed class SqlServerDeleteChange : Change
    {
        private readonly Func<SqlServerDeleteChange, JObject> fac;
        public override int Size => 0;

        public SqlServerDeleteChange(Func<SqlServerDeleteChange, JObject> fac,long token, ChangeType type, Guid id)
            : base(token, type, id)
        {
            this.fac = fac;
        }

        public override JObject CreateEntity() => fac(this);

        public override JsonReader OpenReader() => new JTokenReader(CreateEntity());

    }

    public sealed class FaultyChange : Change
    {
        private readonly Exception exception;
        public override int Size => 0;

        public FaultyChange(long token, ChangeType type, Guid id, Exception exception)
            : base(token, type, id)
        {
            this.exception = exception;
        }

        public override JObject CreateEntity() => new JObject
        {
            ["$exception"] = exception.ToString(),
            ["$faulty"] = true
        };
        public override JsonReader OpenReader() => new JTokenReader(CreateEntity());
    }
}