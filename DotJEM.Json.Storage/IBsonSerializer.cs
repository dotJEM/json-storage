using System.IO;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage
{
    public interface IBsonSerializer
    {
        byte[] Serialize(JObject json);
        JObject Deserialize(byte[] data);
    }

    public class BsonSerializer : IBsonSerializer
    {
        public byte[] Serialize(JObject json)
        {
            MemoryStream stream = new MemoryStream();
            using (BsonWriter writer = new BsonWriter(stream))
            {
                json.WriteTo(writer);
            }
            return stream.ToArray();
        }

        public JObject Deserialize(byte[] data)
        {
            using (BsonReader bson = new BsonReader(new MemoryStream(data)))
            {
                return (JObject)JToken.ReadFrom(bson);
            }
        }
    }
}