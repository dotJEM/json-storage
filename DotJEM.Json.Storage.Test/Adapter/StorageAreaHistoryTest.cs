using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
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
            TestContext.DropArea("historytest");

            IStorageContext context = new SqlServerStorageContext(TestContext.ConnectionString);
            context.Configure.Area("historytest").EnableHistory();

            IStorageArea area = context.Area("historytest");

            JObject create = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            create["unique_field"] = Guid.NewGuid();

            JObject inserted = area.Insert("content", create);
            Guid id = (Guid)inserted["$id"];

            inserted["count"] = 20;
            area.Update(id, inserted);

            Assert.That(area.History.Get(id).ToList(), Has.Count.EqualTo(1));

            JObject v1 = area.History.Get(id, 1);
            Assert.That(v1["count"].Value<int>(), Is.EqualTo(10));
        }

        [Test]
        public void Get_TwoUpdates_ReturnsTwoChanges()
        {
            TestContext.DropArea("historytest");

            IStorageContext context = new SqlServerStorageContext(TestContext.ConnectionString);
            context.Configure.Area("historytest").EnableHistory();

            IStorageArea area = context.Area("historytest");

            JObject create = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            create["unique_field"] = Guid.NewGuid();

            JObject inserted = area.Insert("content", create);
            Guid id = (Guid)inserted["$id"];

            inserted["count"] = 20;
            area.Update(id, inserted);

            inserted["count"] = 30;
            area.Update(id, inserted);

            Assert.That(area.History.Get(id).ToList(), Has.Count.EqualTo(2));
        }

        [Test]
        public void Delete_WithDateTime_DeletesFirstVersion()
        {
            TestContext.DropArea("historytest");

            IStorageContext context = new SqlServerStorageContext(TestContext.ConnectionString);
            context.Configure.Area("historytest").EnableHistory();

            IStorageArea area = context.Area("historytest");

            JObject create = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            create["unique_field"] = Guid.NewGuid();

            JObject inserted = area.Insert("content", create);
            Guid id = (Guid)inserted["$id"];

            Thread.Sleep(2000);

            inserted["count"] = 20;
            DateTime cutoff = (DateTime) area.Update(id, inserted)["$updated"];

            Thread.Sleep(2000);

            inserted["count"] = 30;
            area.Update(id, inserted);
            
            area.History.Delete(cutoff.Subtract(TimeSpan.FromMilliseconds(10)));

            Assert.That(area.History.Get(id).ToList(), Has.Count.EqualTo(1));
        }

        [Test]
        public void Delete_WithTimeSpan_DeletesFirstVersion()
        {
            TestContext.DropArea("historytest");

            IStorageContext context = new SqlServerStorageContext(TestContext.ConnectionString);
            context.Configure.Area("historytest").EnableHistory();

            IStorageArea area = context.Area("historytest");

            JObject create = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            create["unique_field"] = Guid.NewGuid();

            JObject inserted = area.Insert("content", create);
            Guid id = (Guid)inserted["$id"];

            Thread.Sleep(2000);

            inserted["count"] = 20;
            DateTime cutoff = (DateTime)area.Update(id, inserted)["$updated"];

            Thread.Sleep(2000);

            inserted["count"] = 30;
            area.Update(id, inserted);

            area.History.Delete(DateTime.Now - cutoff);

            Assert.That(area.History.Get(id).ToList(), Has.Count.EqualTo(1));
        }

    }
}
