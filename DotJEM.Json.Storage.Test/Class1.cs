using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DotJEM.Json.Storage.Test
{
    [TestFixture]
    public class Class1
    {
        [Test]
        public void Test()
        {
            IStorageContext context = new SqlServerStorageContext("Data Source=.\\DEV;Initial Catalog=json;Integrated Security=True");
            
            IStorageArea table = context.Area("Test");

            //Assert.That(table.Exists, Is.False);

            table.CreateTable();
            Assert.That(table.Exists, Is.True);

            JObject item = table.Insert("item", JObject.Parse("{ name: 'Potatoes' }"));
            JObject item2 = table.Get("item").First();

            Assert.That(item, Is.EqualTo(item2));
        }
    }
}
