using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DotJEM.Json.Storage.Test
{
    [TestFixture]
    public class SqlServerStorageContextTest
    {
        [Test]
        public void CreateTable()
        {
            IStorageContext context = new SqlServerStorageContext("Data Source=.\\DEV;Initial Catalog=json;Integrated Security=True");
            
            IStorageArea table = context.Area("TestArea");

            //Assert.That(table.Exists, Is.False);

            table.CreateTable();
            Assert.That(table.Exists, Is.True);

            JObject item = table.Insert("item", JObject.Parse("{ name: 'Potatoes' }"));
            JObject item2 = table.Get("item").First();

            Assert.That(item, Is.EqualTo(item2));

            JObject item3 = table.Update(item["_id"].ToObject<long>(), "item", item2);
            //Assert.That(item2, Is.EqualTo(item3));

        }
    }
}
