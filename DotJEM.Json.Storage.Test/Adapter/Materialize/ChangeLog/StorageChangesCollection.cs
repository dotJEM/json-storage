using System;
using System.Collections.Generic;
using System.Linq;
using DotJEM.Json.Storage.Adapter.Materialize.ChanceLog;
using DotJEM.Json.Storage.Adapter.Materialize.ChanceLog.ChangeObjects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DotJEM.Json.Storage.Test.Adapter.Materialize.ChangeLog
{
    [TestFixture]
    public class StorageChangesCollectionTest
    {
        [Test]
        public void Partitioned_ReturnsChangesPartitionedIntoCreateUpdateDelete()
        {
            IStorageChangeCollection changes = new StorageChangeCollection("N/A", 0, new List<IChangeLogRow>()
            {
                new DummyChange(00, ChangeType.Update), // 00 -> i=5
                new DummyChange(01, ChangeType.Create), // 01 -> i=0
                new DummyChange(02, ChangeType.Update), // 02 -> i=6
                new DummyChange(03, ChangeType.Delete), // 03 -> i=10
                new DummyChange(04, ChangeType.Update), // 04 -> i=7
                new DummyChange(05, ChangeType.Create), // 05 -> i=1
                new DummyChange(06, ChangeType.Delete), // 06 -> i=11
                new DummyChange(07, ChangeType.Create), // 07 -> i=2
                new DummyChange(08, ChangeType.Delete), // 08 -> i=12
                new DummyChange(09, ChangeType.Update), // 09 -> i=8
                new DummyChange(10, ChangeType.Delete), // 10 -> i=13
                new DummyChange(11, ChangeType.Create), // 11 -> i=3
                new DummyChange(12, ChangeType.Create), // 12 -> i=4
                new DummyChange(13, ChangeType.Update), // 13 -> i=9
                new DummyChange(14, ChangeType.Delete), // 14 -> i=14
            });

            Assert.That(string.Join(":",changes.Partitioned.Select(change => change.Token.ToString("D2"))),
                Is.EqualTo("01:05:07:11:12:00:02:04:09:13:03:06:08:10:14"));

        }

        public class DummyChange : IChangeLogRow
        {

            public string Area { get; } = string.Empty;
            public long Token { get; }
            public int Size { get; } = 0;
            public Guid Id { get; }
            public ChangeType Type { get; }

            public DummyChange(long token, ChangeType type, Guid? id = null)
            {
                Token = token;
                Type = type;
                Id = id ?? Guid.Empty;
            }

            public JObject CreateEntity()
            {
                throw new NotImplementedException();
            }

            public JsonReader OpenReader()
            {
                throw new NotImplementedException();
            }

            public void Dispose()
            {
                throw new NotImplementedException();
            }
        }
    }
}
