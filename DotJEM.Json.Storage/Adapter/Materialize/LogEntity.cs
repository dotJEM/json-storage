using System;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Adapter.Materialize
{
    public class LogEntity
    {
        private readonly Lazy<JObject> value;

        public Guid Id { get; }
        public string ContentType { get; }
        public string Area { get; }
        public long Reference { get; }
        public int Version { get; }

        public DateTime Created { get; }
        public DateTime Updated { get; }
        public JObject Value => value.Value;

        public LogEntity(string area, string contentType, Guid id, long reference, int version, DateTime created, DateTime updated, byte[] data, Func<byte[], JObject> deserialize)
        {
            ContentType = contentType;
            Id = id;
            Reference = reference;
            Area = area;
            Version = version;
            Created = created;
            Updated = updated;
            value = new Lazy<JObject>(() => deserialize(data));
        }



        public static implicit operator JObject(LogEntity entity)
        {
            return entity.Value;
        }
    }
}