using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage
{
    public interface IBsonSerializer
    {
        byte[] Serialize(JObject json);
        JObject Deserialize(byte[] data);
        JsonReader OpenReader(byte[] data);
    }

    public class BsonSerializer : IBsonSerializer
    {
        public byte[] Serialize(JObject json)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BsonDataWriter writer = new BsonDataWriter(stream))
                {
                    json.WriteTo(writer);
                }
                return stream.ToArray();
            }
        }

        public JObject Deserialize(byte[] data)
        {
            if (data.Length == 0)
                return null;

            using (MemoryStream stream = new MemoryStream(data))
            {
                using (BsonDataReader bson = new BsonDataReader(stream))
                {
                    return (JObject)JToken.ReadFrom(bson);
                }
            }
        }

        public JsonReader OpenReader(byte[] data)
        {
            return new BsonDataReader(new MemoryStream(data));
        }
    }
}