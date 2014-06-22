using System;
using System.Linq;
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
            
            IStorageConfiguration config = context.Configuration;
            config.MapField(JsonField.Id, "id");

            config.Area("TestArea2")
                .EnableHistory();

            IStorageArea area = context.Area("TestArea2");


            //cfg.Configure(From.File('asdasd'));
            //cfg.Configure(From.Appconfig());
            //cfg.Configure(For.Area("").EnableHistory());

            //cfg.EnableHistory();
            //cfg.For.Area("").EnableHistory();
            //cfg.From.File("");
            //cfg.From.AppConfig();
            
            //cfg.For.Area("").HistoryDecorator(new JObjectDecorator());
             

            //Assert.That(table.Initialized, Is.False);



            dynamic item = area.Insert("Item", JObject.Parse("{ name: 'Potatoes' }"));
            dynamic item1 = area.Update(item.id.ToObject<Guid>(), "Item", JObject.Parse("{ name: 'Potatoes', count: 10 }"));
            JObject item2 = area.Get("Item").First();

            Assert.That(item, Is.EqualTo(item2));

            JObject item3 = area.Update(item.id.ToObject<Guid>(), "item", item2);
            //Assert.That(item2, Is.EqualTo(item3));

        }
    }
}
