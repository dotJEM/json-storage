using System;
using DotJEM.Json.Storage.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Adapter.Materialize.ChanceLog.ChangeObjects
{
    public class FaultyChangeLogRow : ChangeLogRow
    {
        private readonly Exception exception;
        public override ChangeType Type => ChangeType.Faulty;
        public ChangeType ActualType { get; }
        public override int Size => 0;

        public FaultyChangeLogRow(IStorageContext context, string area, long token, Guid id, ChangeType type, Exception exception)
            : base(context, area, token, id)
        {
            this.exception = exception;
            ActualType = type;
        }


        public override JObject CreateEntity()
        {
            JObject json = new JObject();
            json[Context.Configuration.Fields[JsonField.Id]] = Id;
            json[Context.Configuration.Fields[JsonField.ContentType]] = "$$StorageFault";
            json["$exception"] = exception.ToString();
            json["$exceptionObject"] = JObject.FromObject(exception);
            json["$faulty"] = true;
            return json;
        }

        public override JsonReader OpenReader() => new JTokenReader(CreateEntity());
    }
}