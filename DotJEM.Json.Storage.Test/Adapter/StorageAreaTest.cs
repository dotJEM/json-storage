using System;
using System.Collections.Generic;
using System.Linq;
using DotJEM.Json.Storage.Adapter;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DotJEM.Json.Storage.Test.Adapter
{
    [TestFixture]
    public class StorageAreaTest
    {

        [Test]
        public void Get_100ItemsPage2With10_Returns20to29()
        {
            IStorageContext context = new SqlServerStorageContext(TestContext.ConnectionString);
            IStorageArea area = context.Area("pagingtest");

            for (int i = 1; i < 101; i++) area.Insert("foo", JObject.FromObject(new { i, text = "Just an item" }));

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
            Assert.That(area.Count(), Is.EqualTo(countBefore + 100));
        }

        [Test]
        public void Get_ListOfIds_ReturnsObjects()
        {
            IStorageContext context = new SqlServerStorageContext(TestContext.ConnectionString);
            IStorageArea area = context.Area("pagingtest");

            List<Guid> ids = new List<Guid>();
            for (int i = 1; i < 100; i++)
            {
                Guid id = (Guid)area.Insert("loi", JObject.FromObject(new { i, text = "Just an item" }))["$id"];
                if (i % 3 == 0)
                    ids.Add(id);
            }

            List<JObject> entities = area.Get(ids).ToList();

            Assert.That(entities, Has.Count.EqualTo(33));
        }
    }
}