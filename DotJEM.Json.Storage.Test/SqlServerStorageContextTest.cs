using System;
using System.Collections.Generic;
using System.Linq;
using DotJEM.Json.Storage.Adapter;
using DotJEM.Json.Storage.Configuration;
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
            
            IStorageConfigurator config = context.Configure;
            config.MapField(JsonField.Id, "id");

            config.Area("test")
                .EnableHistory();

            IStorageArea area = context.Area("test");
            dynamic item = area.Insert("Item", JObject.Parse("{ name: 'Potatoes' }"));
            dynamic item1 = area.Update((Guid)item.id, "Item", JObject.Parse("{ name: 'Potatoes', count: 10 }"));
            JObject item2 = area.Get("Item").First();


            Assert.That(item1, Is.EqualTo(item2));
            JObject item3 = area.Update((Guid)item.id, "item", item2);

            IEnumerable<JObject> history = area.History.Get((Guid)item.id);
            Assert.That(history.Count(), Is.EqualTo(2));

            foreach (JObject jObject in history)
            {
                Console.WriteLine(jObject);
            }


        }
    }
}
