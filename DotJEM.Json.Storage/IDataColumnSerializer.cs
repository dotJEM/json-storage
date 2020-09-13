using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage
{
    public interface IDataColumnSerializer
    {
        byte[] Serialize(JObject json);

        void Serialize(JObject json, Stream stream);

        JObject Deserialize(byte[] data);

        JObject Deserialize(Stream stream);

        JsonReader OpenReader(byte[] data);
    }

    public class BsonDataColumnSerializer : IDataColumnSerializer
    {
        public void Serialize(JObject json, Stream stream)
        {
            using BsonDataWriter writer = new BsonDataWriter(stream);
            json.WriteTo(writer);
            writer.Flush();
        }

        public JObject Deserialize(Stream stream)
        {
            using BsonDataReader bson = new BsonDataReader(stream);
            return (JObject)JToken.ReadFrom(bson);
        }

        public byte[] Serialize(JObject json)
        {
            using MemoryStream stream = new MemoryStream();
            Serialize(json, stream);
            return stream.ToArray();
        }

        public JObject Deserialize(byte[] data)
        {
            if (data.Length == 0)
                return null;

            using MemoryStream stream = new MemoryStream(data);
            return Deserialize(stream);
        }

        public JsonReader OpenReader(byte[] data)
        {
            return new BsonDataReader(new MemoryStream(data));
        }
    }
}