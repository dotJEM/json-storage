﻿using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotJEM.Json.Storage.Adapter;
using DotJEM.Json.Storage.Adapter.Materialize.ChanceLog;
using DotJEM.Json.Storage.Adapter.Materialize.Log;
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

            IStorageChangeCollection changes = area.Log.Get(-1);
            JObject create = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            create["unique_field"] = Guid.NewGuid();

            JObject inserted = area.Insert("content", create);

            changes = area.Log.Get(changes.Generation);

            Assert.That(changes.Count(), Is.EqualTo(1));
            Assert.That(changes.Created.Count(), Is.EqualTo(1));

            JObject logEntity = changes.First().CreateEntity();
            Assert.That(logEntity, Is.EqualTo(inserted));

            Console.WriteLine(changes.First().CreateEntity());
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

            IStorageChangeCollection changes = area.Log.Get(-1);

            JObject updated = area.Update((Guid)inserted["$id"], inserted);
            changes = area.Log.Get(changes.Generation);

            Assert.That(changes.Count(), Is.EqualTo(1));
            Assert.That(changes.Updated.Count(), Is.EqualTo(1));
            Assert.That(changes.First().CreateEntity(), Is.EqualTo(updated));

            Console.WriteLine(changes.First().CreateEntity());
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

            IStorageChangeCollection changes = area.Log.Get(-1);

            area.Delete((Guid)inserted["$id"]);
            changes = area.Log.Get(changes.Generation);

            Assert.That(changes.Count(), Is.EqualTo(1));
            Assert.That(changes.Deleted.Count(), Is.EqualTo(1));
            Assert.That(changes.First().CreateEntity(), Is.EqualTo(JObject.Parse("{ $id: '" + inserted["$id"] + "', $contentType: 'Dummy' }")));

            Console.WriteLine(changes.First().CreateEntity());
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

            IStorageChangeCollection changes = area.Log.Get(-1);

            area.Delete((Guid)inserted["$id"]);
            changes = area.Log.Get(changes.Generation, false);

            Assert.That(changes.Count(), Is.EqualTo(0));
        }

        [Test]
        public void Get_MixedUpdates_ReturnsAll()
        {
            IStorageContext context = new SqlServerStorageContext(TestContext.ConnectionString);
            IStorageArea area = context.Area("changelogtest");

            JObject template = JObject.Parse("{ name: 'Dymmy', count: 10 }");

            IStorageChangeCollection changes = area.Log.Get();
            Guid id1 = (Guid)area.Insert("content", template)["$id"];
            Guid id2 = (Guid)area.Insert("content", template)["$id"];
            Guid id3 = (Guid)area.Insert("content", template)["$id"];
            area.Update(id1, template);
            area.Delete(id2);

            changes = area.Log.Get();

            Assert.That(changes.Count, Is.EqualTo(new ChangeCount(1,1,1,0)));
            Assert.That(changes.Created.Count(), Is.EqualTo(1));
            Assert.That(changes.Updated.Count(), Is.EqualTo(1));
            Assert.That(changes.Deleted.Count(), Is.EqualTo(1));

        }
    }
}
