using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Dynamic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using DotJEM.Json.Storage.Configuration;
using DotJEM.Json.Storage.Queries;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Adapter.Materialize
{
    public class JsonEntity : DynamicObject
    {
        public Guid Id { get; protected set; }
        public string ContentType { get; protected set; }

        public DateTime Created { get; protected set; }
        public DateTime Updated { get; protected set; }

        public long Version { get; protected set; }
        public string Reference => reference.Value;
        public string Area { get; protected set; }

        private readonly Lazy<JObject> entity;
        private readonly Lazy<string> reference;

        public JsonEntity(Guid id, string contentType, DateTime created, DateTime updated,
            long version, long reference, string area, Func<JObject> deserialize)
        {
            this.entity = new Lazy<JObject>(deserialize);
            this.reference = new Lazy<string>(() => Base36.Encode(reference));

            Id = id;
            Created = created;
            Updated = updated;
            Version = version;
            ContentType = contentType;
            Area = area;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            //TODO: (jmd 2015-09-24) Use Json Converter?, we also need to fix the binder name?... 
            result = entity.Value[binder.Name];
            return true;
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            //TODO: (jmd 2015-09-24) Use Json Converter, we also need to fix the binder name?... 
            //      Alternatively require that the object is a JToken.
            entity.Value[binder.Name] = JToken.FromObject(value);
            return true;
        }
    }
    
    public class SqlServerJsonEntityFactory
    {
        private readonly SqlServerStorageContext context;

        public SqlServerJsonEntityFactory(SqlServerStorageContext context)
        {
            this.context = context;
        }

        public IEnumerable<JsonEntity> Create(SqlDataReader reader, string area)
        {
            Columns cols = new Columns(reader);

            while (reader.Read())
            {
                ColumnValues values = cols.ReadNext(reader);
                yield return new JsonEntity(
                    values.Id, values.ContentType, values.Created, values.Updated,
                    values.Version, values.Reference, area, () => context.Serializer.Deserialize(values.Data));
            }
        }

        //private JObject CreateJObject(ColumnValues values)
        //{
        //    //JObject json = context.Serializer.Deserialize(values.Data);
        //    //json[context.Configuration.Fields[JsonField.Id]] = values.Id;
        //    //json[context.Configuration.Fields[JsonField.Reference]] = Base36.Encode(values.Reference);
        //    //json[context.Configuration.Fields[JsonField.Area]] = values.Name;
        //    //json[context.Configuration.Fields[JsonField.Version]] = version;
        //    //json[context.Configuration.Fields[JsonField.ContentType]] = contentType;
        //    //json[context.Configuration.Fields[JsonField.Created]] = created;
        //    //json[context.Configuration.Fields[JsonField.Updated]] = updated;
        //    //json = area.Migrate(json);
        //    //return json;
        //}

        //JObject json;
        //    try
        //    {
        //        json = context.Serializer.Deserialize(reader.GetSqlBinary(dataColumn).Value);
        //    }
        //    catch (Exception ex)
        //    {
        //        json = new JObject();
        //        json["$exception"] = ex.ToString();
        //    }


        //    json[context.Configuration.Fields[JsonField.Id]] = reader.GetGuid(idColumn);
        //    json[context.Configuration.Fields[JsonField.Reference]] = Base36.Encode(reader.GetInt64(refColumn));
        //    json[context.Configuration.Fields[JsonField.Area]] = Name;
        //    json[context.Configuration.Fields[JsonField.Version]] = reader.GetInt32(versionColumn);
        //    json[context.Configuration.Fields[JsonField.ContentType]] = reader.GetString(contentTypeColumn);
        //    json[context.Configuration.Fields[JsonField.Created]] = reader.GetDateTime(createdColumn);
        //    json[context.Configuration.Fields[JsonField.Updated]] = reader.GetDateTime(updatedColumn);
        //    return json;

        internal class Columns
        {
            public int Id { get; private set; }
            public int ContentType { get; private set; }
            public int Created { get; private set; }
            public int Updated { get; private set; }
            public int Version { get; private set; }
            public int Reference { get; private set; }
            public int Data { get; private set; }

            public Columns(SqlDataReader reader)
            {
                Data = reader.GetOrdinal(StorageField.Data.ToString());
                Id = reader.GetOrdinal(StorageField.Id.ToString());
                Reference = reader.GetOrdinal(StorageField.Reference.ToString());
                Version = reader.GetOrdinal(StorageField.Version.ToString());
                ContentType = reader.GetOrdinal(StorageField.ContentType.ToString());
                Created = reader.GetOrdinal(StorageField.Created.ToString());
                Updated = reader.GetOrdinal(StorageField.Updated.ToString());
            }

            public Columns(SqlDataReader reader, string prefix)
            {
                Data = reader.GetOrdinal(prefix + "_" + StorageField.Data);
                Id = reader.GetOrdinal(prefix + "_" + StorageField.Id);
                Reference = reader.GetOrdinal(prefix + "_" + StorageField.Reference);
                Version = reader.GetOrdinal(prefix + "_" + StorageField.Version);
                ContentType = reader.GetOrdinal(prefix + "_" + StorageField.ContentType);
                Created = reader.GetOrdinal(prefix + "_" + StorageField.Created);
                Updated = reader.GetOrdinal(prefix + "_" + StorageField.Updated);
            }

            public ColumnValues ReadNext(SqlDataReader reader)
            {
                return new ColumnValues
                {
                    Id = reader.GetGuid(Id),
                    ContentType = reader.GetString(ContentType),
                    Created = reader.GetDateTime(Created),
                    Updated = reader.GetDateTime(Updated),
                    Version = reader.GetInt32(Version),
                    Reference = reader.GetInt64(Reference),
                    Data = reader.GetSqlBinary(Data).Value
                };
            }
        }

        internal class ColumnValues
        {
            public Guid Id { get; set; }
            public string ContentType { get; set; }
            public DateTime Created { get; set; }
            public DateTime Updated { get; set; }
            public int Version { get; set; }
            public long Reference { get; set; }
            public byte[] Data { get; set; }
        }

    }
}
