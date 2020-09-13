using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Adapter.Materialize.ChanceLog.ChangeObjects
{
    public class UpdateChangeLogRow : ChangeLogEntityRow
    {
        private readonly byte[] data;

        public override ChangeType Type => ChangeType.Update;
        public override int Size => data.Length;

        public UpdateChangeLogRow(IStorageContext context, string area, long token, Guid id, string contentType, long reference, int version, DateTime created, DateTime updated,
            byte[] data) : base(context, area, token, id, contentType, reference, version, created, updated)
        {
            this.data = data;
        }

        public override JObject CreateEntity() => AttachProperties(Serializer.Deserialize(data));
        public override JsonReader OpenReader() => Serializer.OpenReader(data);
    }
}