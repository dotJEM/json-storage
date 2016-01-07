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
    public class StorageAreaLogTest
    {
        [Test]
        public void Get_OneCreate_ReturnsOneChange()
        {
            IStorageContext context = new SqlServerStorageContext(TestContext.ConnectionString);
            IStorageArea area = context.Area("changelogtest");

            IStorageChanges changes = area.Log.Get(-1);
            JObject create = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            create["unique_field"] = Guid.NewGuid();

            JObject inserted = area.Insert("content", create);

            changes = area.Log.Get(changes.Token);

            Assert.That(changes.Count(), Is.EqualTo(1));
            Assert.That(changes.Created.Count(), Is.EqualTo(1));
            Assert.That(changes.First().Entity, Is.EqualTo(inserted));

            Console.WriteLine(changes.First().Entity);
        }

        [Test]
        public void Get_OneUpdate_ReturnsOneChange()
        {
            IStorageContext context = new SqlServerStorageContext(TestContext.ConnectionString);
            IStorageArea area = context.Area("changelogtest");

            JObject create = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            create["unique_field"] = Guid.NewGuid();

            JObject inserted = area.Insert("content", create);
            inserted["unique_field"] = Guid.NewGuid();
            
            IStorageChanges changes = area.Log.Get(-1);

            JObject updated = area.Update((Guid)inserted["$id"], inserted);
            changes = area.Log.Get(changes.Token);

            Assert.That(changes.Count(), Is.EqualTo(1));
            Assert.That(changes.Updated.Count(), Is.EqualTo(1));
            Assert.That(changes.First().Entity, Is.EqualTo(updated));

            Console.WriteLine(changes.First().Entity);
        }

        [Test]
        public void Get_OneDelete_ReturnsOneChange()
        {
            IStorageContext context = new SqlServerStorageContext(TestContext.ConnectionString);
            IStorageArea area = context.Area("changelogtest");

            JObject create = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            create["unique_field"] = Guid.NewGuid();

            JObject inserted = area.Insert("content", create);
            inserted["unique_field"] = Guid.NewGuid();

            IStorageChanges changes = area.Log.Get(-1);

            area.Delete((Guid)inserted["$id"]);
            changes = area.Log.Get(changes.Token);

            Assert.That(changes.Count(), Is.EqualTo(1));
            Assert.That(changes.Deleted.Count(), Is.EqualTo(1));
            Assert.That(changes.First().Entity, Is.EqualTo(JObject.Parse("{ $id: '" + inserted["$id"] + "', $contentType: 'Dummy' }")));

            Console.WriteLine(changes.First().Entity);
        }
    }
}
