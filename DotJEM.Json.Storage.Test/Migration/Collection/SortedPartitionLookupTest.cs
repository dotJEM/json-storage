using System;
using System.Collections.Generic;
using DotJEM.Json.Storage.Migration;
using DotJEM.Json.Storage.Migration.Collections;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

// ReSharper disable StringCompareToIsCultureSpecific

namespace DotJEM.Json.Storage.Test.Migration.Collection
{

    [TestFixture]
    public class SortedPartitionLookupTest
    {

        [Test]
        public void Path_0in123_ReturnsOneTwoThree()
        {
            SortedPartitionLookup lookup = new SortedPartitionLookup(new FakeComparer());
            lookup.Add(new DataMigratorEntry("temp", "1", new FakeMigrator("One")));
            lookup.Add(new DataMigratorEntry("temp", "3", new FakeMigrator("Three")));
            lookup.Add(new DataMigratorEntry("temp", "2", new FakeMigrator("Two")));
            Assert.That(lookup.MigrationPath("0"), Has.Count.EqualTo(3));
        }

        [Test]
        public void Path_0in123_ReturnsOrdered()
        {
            SortedPartitionLookup lookup = new SortedPartitionLookup(new FakeComparer());
            lookup.Add(new DataMigratorEntry("temp", "1", new FakeMigrator("1.One")));
            lookup.Add(new DataMigratorEntry("temp", "3", new FakeMigrator("3.Three")));
            lookup.Add(new DataMigratorEntry("temp", "2", new FakeMigrator("2.Two")));
            Assert.That(lookup.MigrationPath("0"), Is.Ordered.Using<FakeMigrator>((a, b) => a.Name.CompareTo(b.Name)));
        }

        [Test]
        public void Path_2in123_ReturnsTwoAndThree()
        {
            SortedPartitionLookup lookup = new SortedPartitionLookup(new FakeComparer());
            lookup.Add(new DataMigratorEntry("temp", "1", new FakeMigrator("1.One")));
            lookup.Add(new DataMigratorEntry("temp", "3", new FakeMigrator("3.Three")));
            lookup.Add(new DataMigratorEntry("temp", "2", new FakeMigrator("2.Two")));
            Assert.That(lookup.MigrationPath("2"), Is.EqualTo(new [] { new FakeMigrator("3.Three") }));
        }

        [Test]
        public void Path_2in134_ReturnsThreeAndFour()
        {
            SortedPartitionLookup lookup = new SortedPartitionLookup(new FakeComparer());
            lookup.Add(new DataMigratorEntry("temp", "3", new FakeMigrator("3.Three")));
            lookup.Add(new DataMigratorEntry("temp", "4", new FakeMigrator("4.Four")));
            lookup.Add(new DataMigratorEntry("temp", "1", new FakeMigrator("1.One")));
            Assert.That(lookup.MigrationPath("2"), Is.EqualTo(new[] { new FakeMigrator("3.Three"), new FakeMigrator("4.Four") }));
        }

        [Test]
        public void Path_2in122234_ReturnsThreeAndFour()
        {
            SortedPartitionLookup lookup = new SortedPartitionLookup(new FakeComparer());
            lookup.Add(new DataMigratorEntry("temp", "1", new FakeMigrator("1.One")));
            lookup.Add(new DataMigratorEntry("temp", "2", new FakeMigrator("2.Two A")));
            lookup.Add(new DataMigratorEntry("temp", "2", new FakeMigrator("2.Two B")));
            lookup.Add(new DataMigratorEntry("temp", "2", new FakeMigrator("2.Two C")));
            lookup.Add(new DataMigratorEntry("temp", "3", new FakeMigrator("3.Three")));
            lookup.Add(new DataMigratorEntry("temp", "4", new FakeMigrator("4.Four")));
            Assert.That(lookup.MigrationPath("2"), Is.EqualTo(new[] { new FakeMigrator("3.Three"), new FakeMigrator("4.Four") }));
        }

        public class FakeComparer : IComparer<DataMigratorEntry>
        {
            public int Compare(DataMigratorEntry x, DataMigratorEntry y)
            {
                return x.Version.CompareTo(y.Version);
            }
        }

        public class FakeMigrator : IDataMigrator, IEquatable<FakeMigrator>
        {
            public string Name { get; set; }

            public FakeMigrator(string name)
            {
                Name = name;
            }

            public JObject Up(JObject source)
            {
                return source;
            }

            public JObject Down(JObject source)
            {
                return source;
            }

            public bool Equals(FakeMigrator other)
            {
                return other.Name == Name;
            }
        }
    }

}
