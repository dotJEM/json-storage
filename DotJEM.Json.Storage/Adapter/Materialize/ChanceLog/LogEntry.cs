using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Adapter.Materialize.Log
{

    public class LogEntry
    {
        //private readonly Lazy<JObject> value;

        public Guid Id { get; }
        public string ContentType { get; }
        public string Area { get; }
        public long Reference { get; }
        public int Version { get; }

        public DateTime Created { get; }
        public DateTime Updated { get; }
        public byte[] Data { get; }
        //public JObject Value => value.Value;

        public LogEntry(string area, string contentType, Guid id, long reference, int version, DateTime created, DateTime updated, byte[] data)
        {
            ContentType = contentType;
            Id = id;
            Reference = reference;
            Area = area;
            Version = version;
            Created = created;
            Updated = updated;
            Data = data;
        }


        public static implicit operator JObject(LogEntry entry)
        {
            return new BsonSerializer().Deserialize(entry.Data);
        }
    }
}