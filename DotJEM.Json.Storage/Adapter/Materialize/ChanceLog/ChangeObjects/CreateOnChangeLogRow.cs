using System;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Adapter.Materialize.ChanceLog.ChangeObjects
{
    // TODO: As it turns out, we could do better here.
    //  - Basically we should not require fields like ContentType, Reference etc in the constructor as they have a cost to compute in this scenario.
    //  - Instead that should be done in a lazy way if we need them.
    //  - 

    public class CreateOnChangeLogRow : ChangeLogEntityRow
    {
        private readonly JObject data;
        public override ChangeType Type => ChangeType.Create;
        public override int Size => Marshal.SizeOf(data);

        public CreateOnChangeLogRow(IStorageContext context, string area, long token, Guid id, string contentType, long reference, int version, DateTime created, DateTime updated, JObject data)
            : base(context, area, token, id, contentType, reference, version, created, updated)
        {
            this.data = data;
        }

        public override JObject CreateEntity() => data;
        public override JsonReader OpenReader() => new JTokenReader(data);
    }

    public class UpdateOnChangeLogRow : ChangeLogEntityRow
    {
        private readonly JObject data;
        public override ChangeType Type => ChangeType.Update;
        public override int Size => Marshal.SizeOf(data);

        public UpdateOnChangeLogRow(IStorageContext context, string area, long token, Guid id, string contentType, long reference, int version, DateTime created, DateTime updated, JObject data)
            : base(context, area, token, id, contentType, reference, version, created, updated)
        {
            this.data = data;
        }

        public override JObject CreateEntity() => data;
        public override JsonReader OpenReader() => new JTokenReader(data);
    }

    public class DeleteOnChangeLogRow : ChangeLogEntityRow
    {
        private readonly JObject data;
        public override ChangeType Type => ChangeType.Delete;
        public override int Size => Marshal.SizeOf(data);

        public DeleteOnChangeLogRow(IStorageContext context, string area, long token, Guid id, string contentType, long reference, int version, DateTime created, DateTime updated, JObject data)
            : base(context, area, token, id, contentType, reference, version, created, updated)
        {
            this.data = data;
        }

        public override JObject CreateEntity() => data;
        public override JsonReader OpenReader() => new JTokenReader(data);
    }
}