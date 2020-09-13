using System;
using DotJEM.Json.Storage.Configuration;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Adapter.Materialize.ChanceLog.ChangeObjects
{
    public abstract class ChangeLogEntityRow : ChangeLogRow
    {
        protected IBsonSerializer Serializer => Context.Serializer;

        public string ContentType { get; }
        public long Reference { get; }
        public int Version { get; }
        public DateTime Created { get; }
        public DateTime Updated { get; }

        protected ChangeLogEntityRow(IStorageContext context, string area, long token, Guid id, string contentType, long reference, int version, DateTime created, DateTime updated) 
            : base(context, area, token, id)
        {
            ContentType = contentType;
            Reference = reference;
            Version = version;
            Created = created;
            Updated = updated;
        }

        protected JObject Migrate(JObject input)
        {
            Context.MigrationManager.Upgrade(ref input);
            return input;
        }

        protected JObject AttachProperties(JObject json)
        {
            json[Context.Configuration.Fields[JsonField.Id]] = Id;
            json[Context.Configuration.Fields[JsonField.Reference]] = Base36.Encode(Reference);
            json[Context.Configuration.Fields[JsonField.Area]] = Area;
            json[Context.Configuration.Fields[JsonField.Version]] = Version;
            json[Context.Configuration.Fields[JsonField.ContentType]] = ContentType;
            json[Context.Configuration.Fields[JsonField.Created]] = DateTime.SpecifyKind(Created, DateTimeKind.Utc);
            json[Context.Configuration.Fields[JsonField.Updated]] = DateTime.SpecifyKind(Updated, DateTimeKind.Utc);
            return Migrate(json);
        }
    }
}