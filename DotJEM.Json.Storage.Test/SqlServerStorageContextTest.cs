using System;
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
            
            IStorageArea area = context.Area("GuidBasedIds");
            var cfg = context.Config;

            //cfg.Configure(From.File('asdasd'));
            //cfg.Configure(From.Appconfig());
            //cfg.Configure(For.Area("").EnableHistory());

            //cfg.EnableHistory();
            //cfg.For.Area("").EnableHistory();
            //cfg.From.File("");
            //cfg.From.AppConfig();
            
            //cfg.For.Area("").HistoryDecorator(new JObjectDecorator());
             

            //Assert.That(table.Initialized, Is.False);

            area.Initialize();
            area.CreateHistoryTable();
            Assert.That(area.Initialized, Is.True);

            JObject item = area.Insert("item", JObject.Parse("{ name: 'Potatoes' }"));
            JObject item2 = area.Get("item").First();

            Assert.That(item, Is.EqualTo(item2));

            JObject item3 = area.Update(item["_id"].ToObject<Guid>(), "item", item2);
            //Assert.That(item2, Is.EqualTo(item3));

        }
    }
}
