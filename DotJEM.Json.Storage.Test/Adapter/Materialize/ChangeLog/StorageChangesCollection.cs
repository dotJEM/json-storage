using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using DotJEM.Json.Storage.Adapter.Materialize.ChanceLog;
using DotJEM.Json.Storage.Adapter.Materialize.Log;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DotJEM.Json.Storage.Test.Adapter.Materialize.ChangeLog
{
    [TestFixture]
    public class StorageChangesCollectionTest
    {
        [Test, Repeat(50)]
        public void Count_RandomData_MatchesItems()
        {
            IStorageChangeCollection changes = new StorageChangeCollection("N/A", 0, new List<Change>(new FakeChangeFactory().Random(100)));


            Assert.That(changes.Count, Is.EqualTo(new ChangeCount(changes.Created.Count(), changes.Updated.Count(), changes.Deleted.Count())));
        }

        [Test]
        public void Count_5Each_ReturnsCountWith5Each()
        {
            IStorageChangeCollection changes = new StorageChangeCollection("N/A", 0, new List<Change>()
            {
                new FakeChange(00, ChangeType.Update, Guid.Empty), // 00 -> i=5
                new FakeChange(01, ChangeType.Create, Guid.Empty), // 01 -> i=0
                new FakeChange(02, ChangeType.Update, Guid.Empty), // 02 -> i=6
                new FakeChange(03, ChangeType.Delete, Guid.Empty), // 03 -> i=10
                new FakeChange(04, ChangeType.Update, Guid.Empty), // 04 -> i=7
                new FakeChange(05, ChangeType.Create, Guid.Empty), // 05 -> i=1
                new FakeChange(06, ChangeType.Delete, Guid.Empty), // 06 -> i=11
                new FakeChange(07, ChangeType.Create, Guid.Empty), // 07 -> i=2
                new FakeChange(08, ChangeType.Delete, Guid.Empty), // 08 -> i=12
                new FakeChange(09, ChangeType.Update, Guid.Empty), // 09 -> i=8
                new FakeChange(10, ChangeType.Delete, Guid.Empty), // 10 -> i=13
                new FakeChange(11, ChangeType.Create, Guid.Empty), // 11 -> i=3
                new FakeChange(12, ChangeType.Create, Guid.Empty), // 12 -> i=4
                new FakeChange(13, ChangeType.Update, Guid.Empty), // 13 -> i=9
                new FakeChange(14, ChangeType.Delete, Guid.Empty), // 14 -> i=14
            });

            Assert.That(changes.Count, Is.EqualTo(new ChangeCount(5, 5, 5)));
        }

        [Test]
        public void Partitioned_ReturnsChangesPartitionedIntoCreateUpdateDelete()
        {
            IStorageChangeCollection changes = new StorageChangeCollection("N/A", 0, new List<Change>()
            {
                new FakeChange(00, ChangeType.Update, Guid.Empty), // 00 -> i=5
                new FakeChange(01, ChangeType.Create, Guid.Empty), // 01 -> i=0
                new FakeChange(02, ChangeType.Update, Guid.Empty), // 02 -> i=6
                new FakeChange(03, ChangeType.Delete, Guid.Empty), // 03 -> i=10
                new FakeChange(04, ChangeType.Update, Guid.Empty), // 04 -> i=7
                new FakeChange(05, ChangeType.Create, Guid.Empty), // 05 -> i=1
                new FakeChange(06, ChangeType.Delete, Guid.Empty), // 06 -> i=11
                new FakeChange(07, ChangeType.Create, Guid.Empty), // 07 -> i=2
                new FakeChange(08, ChangeType.Delete, Guid.Empty), // 08 -> i=12
                new FakeChange(09, ChangeType.Update, Guid.Empty), // 09 -> i=8
                new FakeChange(10, ChangeType.Delete, Guid.Empty), // 10 -> i=13
                new FakeChange(11, ChangeType.Create, Guid.Empty), // 11 -> i=3
                new FakeChange(12, ChangeType.Create, Guid.Empty), // 12 -> i=4
                new FakeChange(13, ChangeType.Update, Guid.Empty), // 13 -> i=9
                new FakeChange(14, ChangeType.Delete, Guid.Empty), // 14 -> i=14
            });

            Assert.That(string.Join(":", changes.Partitioned.Select(change => change.Generation.ToString("D2"))),
                Is.EqualTo("01:05:07:11:12:00:02:04:09:13:03:06:08:10:14"));
        }

        public class FakeChange : Change
        {
            public override int Size { get; }
            public FakeChange(long generation, ChangeType type, Guid id) : base(generation, type, id)
            {
            }

            public override JObject CreateEntity()
            {
                throw new NotImplementedException();
            }

            public override JsonReader OpenReader()
            {
                throw new NotImplementedException();
            }
        }

        public class FakeChangeFactory
        {
            private long gen = 0;
            private readonly Random rnd = new Random();
            private readonly ChangeType[] distribution;

            public FakeChangeFactory()
                : this(ChangeType.Create, ChangeType.Update, ChangeType.Delete)
            {
            }

            public FakeChangeFactory(params ChangeType[] distribution)
            {
                this.distribution = distribution;
            }

            public FakeChangeFactory(int create, int update, int delete)
                : this(Enumerable.Empty<ChangeType>()
                      .Union(Enumerable.Repeat(ChangeType.Create, create))
                      .Union(Enumerable.Repeat(ChangeType.Update, update))
                      .Union(Enumerable.Repeat(ChangeType.Delete, delete))
                      .ToArray())
            {
            }

            public Change Random()
            {
                return new FakeChange(gen++, distribution[rnd.Next(distribution.Length)], Guid.NewGuid());
            }

            public IEnumerable<Change> Random(int count)
            {
                for (int i = 0; i < count; i++)
                    yield return Random();
            }
        }
    }


}
