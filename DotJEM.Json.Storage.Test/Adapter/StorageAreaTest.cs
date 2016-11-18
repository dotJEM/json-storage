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

        [Test]
        public void Get_OneDeleteNoDeletes_ReturnsZeroChange()
        {
            IStorageContext context = new SqlServerStorageContext(TestContext.ConnectionString);
            IStorageArea area = context.Area("changelogtest");

            JObject create = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            create["unique_field"] = Guid.NewGuid();

            JObject inserted = area.Insert("content", create);
            inserted["unique_field"] = Guid.NewGuid();

            IStorageChanges changes = area.Log.Get(-1);

            area.Delete((Guid)inserted["$id"]);
            changes = area.Log.Get(changes.Token, false);

            Assert.That(changes.Count(), Is.EqualTo(0));
        }

        [Test]
        public void Get_100ItemsPage2With10_Returns20to29()
        {
            IStorageContext context = new SqlServerStorageContext(TestContext.ConnectionString);
            IStorageArea area = context.Area("pagingtest");

            for(int i = 1; i < 101; i++) area.Insert("foo", JObject.FromObject(new { i, text = "Just an item"}));

            List<JObject> ten = area.Get(20, 10).ToList();
            
            Assert.That(ten, Has.Count.EqualTo(10)
                & Has.None.Matches<dynamic>(json => json.i == 19)
                & Has.Some.Matches<dynamic>(json => json.i == 20)
                & Has.Some.Matches<dynamic>(json => json.i == 21)
                & Has.Some.Matches<dynamic>(json => json.i == 22)
                & Has.Some.Matches<dynamic>(json => json.i == 23)
                & Has.Some.Matches<dynamic>(json => json.i == 24)
                & Has.Some.Matches<dynamic>(json => json.i == 25)
                & Has.Some.Matches<dynamic>(json => json.i == 26)
                & Has.Some.Matches<dynamic>(json => json.i == 27)
                & Has.Some.Matches<dynamic>(json => json.i == 28)
                & Has.Some.Matches<dynamic>(json => json.i == 29)
                & Has.None.Matches<dynamic>(json => json.i == 30));
        }

        [Test]
        public void Get_Page_ReturnsZeroChange()
        {
            IStorageContext context = new SqlServerStorageContext(TestContext.ConnectionString);
            IStorageArea area = context.Area("pagingtest");

            for (int i = 1; i < 101; i++) area.Insert("fiz", JObject.FromObject(new { i, text = "Just an item" }));

            List<JObject> ten = area.Get("fiz", 20, 10).ToList();

            Assert.That(ten, Has.Count.EqualTo(10)
                & Has.None.Matches<dynamic>(json => json.i == 19)
                & Has.Some.Matches<dynamic>(json => json.i == 20)
                & Has.Some.Matches<dynamic>(json => json.i == 21)
                & Has.Some.Matches<dynamic>(json => json.i == 22)
                & Has.Some.Matches<dynamic>(json => json.i == 23)
                & Has.Some.Matches<dynamic>(json => json.i == 24)
                & Has.Some.Matches<dynamic>(json => json.i == 25)
                & Has.Some.Matches<dynamic>(json => json.i == 26)
                & Has.Some.Matches<dynamic>(json => json.i == 27)
                & Has.Some.Matches<dynamic>(json => json.i == 28)
                & Has.Some.Matches<dynamic>(json => json.i == 29)
                & Has.None.Matches<dynamic>(json => json.i == 30));
        }

        [Test]
        public void Count_All_Returns100()
        {
            IStorageContext context = new SqlServerStorageContext(TestContext.ConnectionString);
            IStorageArea area = context.Area("pagingtest");

            long countBefore = area.Count();
            for (int i = 1; i < 101; i++) area.Insert("fiz", JObject.FromObject(new { i, text = "Just an item" }));

            List<JObject> ten = area.Get("fiz", 20, 10).ToList();

            Assert.That(area.Count(), Is.EqualTo(countBefore+100));
        }
    }
}
