using System;
using System.Collections;
using System.Collections.Generic;
using DotJEM.Json.Storage.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Adapter.Materialize.ChanceLog.ChangeObjects
{
    public class DeleteChangeLogRow : ChangeLogRow
    {
        public override ChangeType Type => ChangeType.Delete;
        public override int Size => 0;

        public DeleteChangeLogRow(IStorageContext context, string area, long token, Guid id)
            : base(context, area, token, id)
        {
        }

        public override JObject CreateEntity()
        {
            JObject json = new JObject();
            json[Context.Configuration.Fields[JsonField.Id]] = Id;
            json[Context.Configuration.Fields[JsonField.ContentType]] = "Dummy";
            return json;
        }

        public override JsonReader OpenReader() => new JTokenReader(CreateEntity());
    }
}