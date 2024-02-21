using System;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Adapter.Materialize.ChanceLog.ChangeObjects
{
    public class CreateOnChangeLogRow : ChangeLogEntityRow
    {
        private readonly JObject data;
        public override ChangeType Type => ChangeType.Create;
        public override int Size { get; }

        public CreateOnChangeLogRow(IStorageContext context, string area, long token, Guid id, string contentType, long reference, int version, DateTime created, DateTime updated, JObject data, int size)
            : base(context, area, token, id, contentType, reference, version, created, updated)
        {
            this.data = data;
            this.Size = size;
        }

        public override JObject CreateEntity() => data;
        public override JsonReader OpenReader() => new JTokenReader(data);
    }

    public class UpdateOnChangeLogRow : ChangeLogEntityRow
    {
        private readonly JObject data;
        public override ChangeType Type => ChangeType.Update;
        public override int Size { get; }

        public UpdateOnChangeLogRow(IStorageContext context, string area, long token, Guid id, string contentType, long reference, int version, DateTime created, DateTime updated, JObject data, int size)
            : base(context, area, token, id, contentType, reference, version, created, updated)
        {
            this.data = data;
            this.Size = size;
        }

        public override JObject CreateEntity() => data;
        public override JsonReader OpenReader() => new JTokenReader(data);
    }

    public class DeleteOnChangeLogRow : ChangeLogEntityRow
    {
        private readonly JObject data;
        public override ChangeType Type => ChangeType.Delete;
        public override int Size { get; }

        public DeleteOnChangeLogRow(IStorageContext context, string area, long token, Guid id, string contentType, long reference, int version, DateTime created, DateTime updated, JObject data, int size)
            : base(context, area, token, id, contentType, reference, version, created, updated)
        {
            this.data = data;
            this.Size = size;
        }

        public override JObject CreateEntity() => data;
        public override JsonReader OpenReader() => new JTokenReader(data);
    }
}