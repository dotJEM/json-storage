using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Transactions;
using DotJEM.Json.Storage.Adapter;
using DotJEM.Json.Storage.Adapter.Materialize.ChanceLog;
using DotJEM.Json.Storage.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DotJEM.Json.Storage.Test
{
    [TestFixture]
    public class SqlServerStorageContextTest
    {
        [TestCase("item")]
        [TestCase("other")]
        [TestCase("data")]
        public void CreateTable(string contentType)
        {
            IStorageContext context = new SqlServerStorageContext(TestContext.ConnectionString);
            
            IStorageConfigurator config = context.Configure;
            config.MapField(JsonField.Id, "id");

            config.Area("test")
                .EnableHistory();

            IStorageArea area = context.Area("test");

            Enumerable.Range(0, 100)./*AsParallel().*/Select(number =>
            {
                GetValue(contentType, area);
                return number;
            }).ToArray();
        }

        [Test]
        public void Fo()
        {
            IStorageContext context = new SqlServerStorageContext(TestContext.ConnectionString);
            context.Configure
                .MapField(JsonField.Id, "id")
                .Area("info-test1")
                .EnableHistory();

            context.Configure.Area("info-test2")
                .EnableHistory();

            JObject doc = context.Area("info-test1").Insert("dummy", new JObject());
            context.Area("info-test1").Update((Guid) doc["id"], new JObject()); // force creation of history table.
            context.Area("info-test2").Insert("dummy", new JObject());
            context.Area("info-test3").Insert("dummy", new JObject());

            IAreaInformationCollection areas = context.AreaInfos;


            Assert.That(areas["info-test1"].Tables, Is.EquivalentTo(new []{ "info-test1", "info-test1.seed", "info-test1.changelog", "info-test1.history" }));
            Assert.That(areas["info-test2"].Tables, Is.EquivalentTo(new []{ "info-test2", "info-test2.seed", "info-test2.changelog" })); //There is no history table before it's needed.
            Assert.That(areas["info-test3"].Tables, Is.EquivalentTo(new []{ "info-test3", "info-test3.seed", "info-test3.changelog" }));
        }

        //[Test]  //TODO: TransactionScope does not work as it tries to elevate the transaction to a trx service.
        //                The TransactionScope class uses the CallContext and the logical setters/getters, so it is possible to implement our own simplified version that
        //                focuses on transactions that only runs locally.
        //public void TestTransactionScope()
        //{
        //    IStorageContext context = new SqlServerStorageContext(TestContext.ConnectionString);
        //    context.Configure.Area("transactions").EnableHistory();

        //    IStorageArea area = context.Area("transactions");


        //    using (var scope = new TransactionScope())
        //    {
        //        area.Insert("foo", JObject.FromObject(JObject.Parse("{ name: 'Potatoes' }")));
        //        scope.Complete();
        //    }

        //    Assert.That(area.Get().Count(), Is.EqualTo(0));

        //}
        
        [Test]
        public void CustomEncryptingSerializerTest()
        {
            SqlServerStorageContext context = new SqlServerStorageContext(TestContext.ConnectionString, new RijndaelBsonDataColumnSerializer());
            context.Configure.MapField(JsonField.Id, "id");

            IStorageArea area = context.Area("encrypted");
            Guid id = (Guid) area.Insert("user", JObject.Parse("{ name: 'Peter', occupation: 'Killer' }"))["id"];
            JObject user = area.Get(id);

            Assert.That((string)user["name"], Is.EqualTo("Peter"));

            SqlServerStorageContext context2 = new SqlServerStorageContext(TestContext.ConnectionString, new BsonDataColumnSerializer());
            context2.Configure.MapField(JsonField.Id, "id");

            JObject errored = context2.Area("encrypted").Get(id);
            Assert.That((string)errored["$exception"], Is.Not.Null);
        }

        private static void GetValue(string contentType, IStorageArea area)
        {
            dynamic item = area.Insert(contentType, JObject.Parse("{ name: 'Potatoes' }"));
            dynamic item1 = area.Update((Guid) item.id, JObject.Parse("{ name: 'Potatoes', count: 10 }"));
            JObject item2 = area.Get(contentType).First();

            Assert.That(item1, Is.EqualTo(item2));
            JObject item3 = area.Update((Guid) item.id, item2);

            //IEnumerable<JObject> history = area.History.Get((Guid) item.id);
            //Assert.That(history.Count(), Is.EqualTo(2));

            //foreach (JObject jObject in history)
            //{
            //    Console.WriteLine(jObject);
            //}
        }
    }

    public class RijndaelBsonDataColumnSerializer : IDataColumnSerializer
    {
        private readonly BsonDataColumnSerializer inner = new BsonDataColumnSerializer();
        private readonly RijndaelManaged crypto = new RijndaelManaged()
        {
            //BlockSize = 256, Mode = CipherMode.CBC, Padding = PaddingMode.PKCS7
        };  


        public byte[] Serialize(JObject json)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                Serialize(json, stream);
                return stream.ToArray();
            }
        }

        public void Serialize(JObject json, Stream stream)
        {
            // see: https://stackoverflow.com/questions/10168240/encrypting-decrypting-a-string-in-c-sharp for a better way
            byte[] key = {0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16};  
            byte[] iv = {0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16};
            using (ICryptoTransform encryptor = crypto.CreateEncryptor(key, iv))
            {
                using (var cryptoStream = new CryptoStream(stream, encryptor, CryptoStreamMode.Write))
                {
                    inner.Serialize(json, cryptoStream);
                    cryptoStream.Close();
                }
            }
        }

        public JObject Deserialize(byte[] data)
        {
            using (MemoryStream stream = new MemoryStream(data))
            {
                return Deserialize(stream);
            }
        }

        public JObject Deserialize(Stream stream)
        {
            // see: https://stackoverflow.com/questions/10168240/encrypting-decrypting-a-string-in-c-sharp for a better way
            byte[] key = {0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16};  
            byte[] iv = {0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16};
            using (ICryptoTransform decryptor = crypto.CreateDecryptor(key, iv))
            {
                using (var cryptoStream = new CryptoStream(stream, decryptor, CryptoStreamMode.Read))
                {
                    return inner.Deserialize(cryptoStream);
                }
            }
        }

        public JsonReader OpenReader(byte[] data)
        {

            return null;
        }
    }
}
