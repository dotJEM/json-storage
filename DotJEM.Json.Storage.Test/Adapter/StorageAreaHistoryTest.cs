using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotJEM.Json.Storage.Adapter;
using DotJEM.Json.Storage.Configuration;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DotJEM.Json.Storage.Test.Adapter
{
    [TestFixture]
    public class StorageAreaHistoryTest
    {
        [Test]
        public void Get_OneCreate_ReturnsOneChange()
        {
            IStorageContext context = new SqlServerStorageContext(TestContext.ConnectionString);
            context.Configure.Area("historytest").EnableHistory();

            IStorageArea area = context.Area("historytest");

            JObject create = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            create["unique_field"] = Guid.NewGuid();

            JObject inserted = area.Insert("content", create);
            Guid id = (Guid) inserted["$id"];

            inserted["count"] = 20;
            area.Update(id, inserted);

            Assert.That(area.History.Get(id).ToList(), Has.Count.EqualTo(1));

            JObject v1 = area.History.Get(id, 1);
            Assert.That(v1["count"].Value<int>(), Is.EqualTo(10));
        }

    }
}
