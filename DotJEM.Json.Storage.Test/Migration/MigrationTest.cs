using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using DotJEM.Json.Storage.Adapter;
using DotJEM.Json.Storage.Migration;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DotJEM.Json.Storage.Test.Migration
{
    [TestFixture]
    class MigrationTest
    {
        private static readonly string ConnectionString = TestContext.ConnectionString;
        private static readonly string EntityTableName = "myEntities";
        private static readonly string SchemaVersionProperty = "$schemaVersion";
        private static readonly string IdProperty = "$id";
        private static readonly string DefaultVersion = "5";
        private static readonly string OldVersion = "2";

        private TestVersionProvider currentVersionProvider;
        private TestVersionProvider oldVersionProvider;


        [SetUp]
        public void SetupTest()
        {
            currentVersionProvider = new TestVersionProvider {Current = DefaultVersion};
            oldVersionProvider = new TestVersionProvider {Current = OldVersion};

            // Clean up the database
            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand { Connection = connection })
                {
                    try
                    {
                        command.CommandText = "DELETE FROM " + EntityTableName + " ;";
                        command.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                    }
                }
            }

        }


        [Test]
        public void WhenEntityIsInserted_EntityIsUpdatedWithSchemaVersion()
        {
            // Arrange
            IStorageContext context = new SqlServerStorageContext(ConnectionString);
            context.Configure.VersionProvider = currentVersionProvider;

            IStorageArea area = context.Area(EntityTableName);

            JObject newEntity = JObject.Parse("{ name: 'Potatoes', count: 10 }");

            // Act
            JObject result = area.Insert("content", newEntity);

            // Assert
            Assert.That(result.Property("name") != null, Is.True);
            Assert.That(result.Property("name").Value.ToString(), Is.EqualTo("Potatoes"));
            Assert.That(result.Property("count") != null, Is.True);
            Assert.That(result.Property("count").Value.ToString(), Is.EqualTo("10"));
            Assert.That(result.Property(SchemaVersionProperty) != null, Is.True);
            Assert.That(result.Property(SchemaVersionProperty).Value.ToString(), Is.EqualTo(DefaultVersion));
        }

        [Test]
        public void NoMigratorsDefined_ObjectIsNotChanged()
        {
            // Arrange
            IStorageContext context = new SqlServerStorageContext(ConnectionString);
            context.Configure.VersionProvider = currentVersionProvider;

            IStorageArea area = context.Area(EntityTableName);

            JObject newEntity = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            JObject inserted = area.Insert("content", newEntity);
            Assert.That(inserted.Property(SchemaVersionProperty) != null, Is.True);
            Assert.That(inserted.Property(SchemaVersionProperty).Value.ToString(), Is.EqualTo(DefaultVersion));

            // Act
            IEnumerable<JObject> result = area.Get("content");

            // Assert
            Assert.That(result.Count(), Is.EqualTo(1));
            JObject jObject = result.First();
            Assert.That(jObject.Property("name") != null, Is.True);
            Assert.That(jObject.Property("name").Value.ToString(), Is.EqualTo("Potatoes"));
            Assert.That(jObject.Property("count") != null, Is.True);
            Assert.That(jObject.Property("count").Value.ToString(), Is.EqualTo("10"));
            Assert.That(jObject.Property(SchemaVersionProperty) != null, Is.True);
            Assert.That(jObject.Property(SchemaVersionProperty).Value.ToString(), Is.EqualTo(DefaultVersion));
        }

        [Test]
        public void AnAddMigratorIsDefined_ObjectIsUpdated()
        {
            // Arrange
            IStorageContext context = new SqlServerStorageContext(ConnectionString);
            context.Configure.VersionProvider = oldVersionProvider;

            IStorageArea area = context.Area(EntityTableName);

            JObject newEntity = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            JObject inserted = area.Insert("content", newEntity);
            Assert.That(inserted.Property(SchemaVersionProperty) != null, Is.True);
            Assert.That(inserted.Property(SchemaVersionProperty).Value.ToString(), Is.EqualTo(OldVersion));
            context.Release(EntityTableName);

            context = new SqlServerStorageContext(ConnectionString);
            context.Configure.VersionProvider = currentVersionProvider;
            context.MigrationManager.Add(new AddAttributeForVersion4Migrator("newAttribute", "attributeValue"));
            area = context.Area(EntityTableName);

            // Act
            IEnumerable<JObject> result = area.Get("content");

            // Assert
            Assert.That(result.Count(), Is.EqualTo(1));
            JObject jObject = result.First();
            Assert.That(jObject.Property("name") != null, Is.True);
            Assert.That(jObject.Property("name").Value.ToString(), Is.EqualTo("Potatoes"));
            Assert.That(jObject.Property("count") != null, Is.True);
            Assert.That(jObject.Property("count").Value.ToString(), Is.EqualTo("10"));
            Assert.That(jObject.Property("newAttribute") != null, Is.True);
            Assert.That(jObject.Property("newAttribute").Value.ToString(), Is.EqualTo("attributeValue"));
            Assert.That(jObject.Property(SchemaVersionProperty) != null, Is.True);
            Assert.That(jObject.Property(SchemaVersionProperty).Value.ToString(), Is.EqualTo(DefaultVersion));
        }

        [Test]
        public void ARenameMigratorIsDefined_ObjectIsUpdated()
        {
            // Arrange
            IStorageContext context = new SqlServerStorageContext(ConnectionString);
            context.Configure.VersionProvider = oldVersionProvider;

            IStorageArea area = context.Area(EntityTableName);

            JObject newEntity = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            JObject inserted = area.Insert("content", newEntity);
            Assert.That(inserted.Property(SchemaVersionProperty) != null, Is.True);
            Assert.That(inserted.Property(SchemaVersionProperty).Value.ToString(), Is.EqualTo(OldVersion));
            context.Release(EntityTableName);

            context = new SqlServerStorageContext(ConnectionString);
            context.Configure.VersionProvider = currentVersionProvider;
            context.MigrationManager.Add(new RenameAttributeForVersion3Migrator("count", "size"));
            area = context.Area(EntityTableName);

            // Act
            IEnumerable<JObject> result = area.Get("content");

            // Assert
            Assert.That(result.Count(), Is.EqualTo(1));
            JObject jObject = result.First();
            Assert.That(jObject.Property("name") != null, Is.True);
            Assert.That(jObject.Property("name").Value.ToString(), Is.EqualTo("Potatoes"));
            Assert.That(jObject.Property("count") != null, Is.False);
            Assert.That(jObject.Property("size") != null, Is.True);
            Assert.That(jObject.Property("size").Value.ToString(), Is.EqualTo("10"));
            Assert.That(jObject.Property(SchemaVersionProperty) != null, Is.True);
            Assert.That(jObject.Property(SchemaVersionProperty).Value.ToString(), Is.EqualTo(DefaultVersion));
        }

        [Test]
        public void AMigratorIsDefined_ObjectHasAnOldSchemaVersion_ObjectIsUpdated()
        {
            // Arrange
            IStorageContext context = new SqlServerStorageContext(ConnectionString);
            context.Configure.VersionProvider = oldVersionProvider;

            IStorageArea area = context.Area(EntityTableName);

            JObject newEntity = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            area.Insert("content", newEntity);
            context.Release(EntityTableName);

            context = new SqlServerStorageContext(ConnectionString);
            context.Configure.VersionProvider = currentVersionProvider;
            context.MigrationManager.Add(new AddAttributeForVersion4Migrator("newAttribute", "attributeValue"));
            area = context.Area(EntityTableName);

            // Act
            IEnumerable<JObject> result = area.Get("content");

            // Assert
            Assert.That(result.Count(), Is.EqualTo(1));
            JObject jObject = result.First();
            Assert.That(jObject.Property("name") != null, Is.True);
            Assert.That(jObject.Property("name").Value.ToString(), Is.EqualTo("Potatoes"));
            Assert.That(jObject.Property("count") != null, Is.True);
            Assert.That(jObject.Property("count").Value.ToString(), Is.EqualTo("10"));
            Assert.That(jObject.Property("newAttribute") != null, Is.True);
            Assert.That(jObject.Property("newAttribute").Value.ToString(), Is.EqualTo("attributeValue"));
            Assert.That(jObject.Property(SchemaVersionProperty) != null, Is.True);
            Assert.That(jObject.Property(SchemaVersionProperty).Value.ToString(), Is.EqualTo(DefaultVersion));
        }

        [Test]
        public void TwoMigratorsAreDefined_ObjectIsUpdatedByBoth()
        {
            // Arrange
            IStorageContext context = new SqlServerStorageContext(ConnectionString);
            context.Configure.VersionProvider = oldVersionProvider;

            IStorageArea area = context.Area(EntityTableName);

            JObject newEntity = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            JObject inserted = area.Insert("content", newEntity);
            Assert.That(inserted.Property(SchemaVersionProperty) != null, Is.True);
            Assert.That(inserted.Property(SchemaVersionProperty).Value.ToString(), Is.EqualTo(OldVersion));
            context.Release(EntityTableName);

            context = new SqlServerStorageContext(ConnectionString);
            context.Configure.VersionProvider = currentVersionProvider;
            context.MigrationManager.Add(new AddAttributeForVersion4Migrator("newAttribute", "attributeValue"));
            context.MigrationManager.Add(new RenameAttributeForVersion3Migrator("count", "size"));
            area = context.Area(EntityTableName);

            // Act
            IEnumerable<JObject> result = area.Get("content");

            // Assert
            Assert.That(result.Count(), Is.EqualTo(1));
            JObject jObject = result.First();
            Assert.That(jObject.Property("name") != null, Is.True);
            Assert.That(jObject.Property("name").Value.ToString(), Is.EqualTo("Potatoes"));
            Assert.That(jObject.Property("count") != null, Is.False);
            Assert.That(jObject.Property("size") != null, Is.True);
            Assert.That(jObject.Property("size").Value.ToString(), Is.EqualTo("10"));
            Assert.That(jObject.Property("newAttribute") != null, Is.True);
            Assert.That(jObject.Property("newAttribute").Value.ToString(), Is.EqualTo("attributeValue"));
            Assert.That(jObject.Property(SchemaVersionProperty) != null, Is.True);
            Assert.That(jObject.Property(SchemaVersionProperty).Value.ToString(), Is.EqualTo(DefaultVersion));
        }

        [Test]
        public void TwoMigratorsAreDefined_ObjectIsUpdatedByOnlyOne()
        {
            // Arrange
            IStorageContext context = new SqlServerStorageContext(ConnectionString);
            context.Configure.VersionProvider = new TestVersionProvider() { Current = "3"};

            IStorageArea area = context.Area(EntityTableName);

            JObject newEntity = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            JObject inserted = area.Insert("content", newEntity);
            Assert.That(inserted.Property(SchemaVersionProperty) != null, Is.True);
            Assert.That(inserted.Property(SchemaVersionProperty).Value.ToString(), Is.EqualTo("3"));
            context.Release(EntityTableName);

            context = new SqlServerStorageContext(ConnectionString);
            context.Configure.VersionProvider = currentVersionProvider;
            context.MigrationManager.Add(new AddAttributeForVersion4Migrator("newAttribute", "attributeValue"));
            context.MigrationManager.Add(new RenameAttributeForVersion3Migrator("count", "size"));
            area = context.Area(EntityTableName);

            // Act
            IEnumerable<JObject> result = area.Get("content");

            // Assert
            Assert.That(result.Count(), Is.EqualTo(1));
            JObject jObject = result.First();
            Assert.That(jObject.Property("name") != null, Is.True);
            Assert.That(jObject.Property("name").Value.ToString(), Is.EqualTo("Potatoes"));
            Assert.That(jObject.Property("count") != null, Is.True);
            Assert.That(jObject.Property("count").Value.ToString(), Is.EqualTo("10"));
            Assert.That(jObject.Property("newAttribute") != null, Is.True);
            Assert.That(jObject.Property("newAttribute").Value.ToString(), Is.EqualTo("attributeValue"));
            Assert.That(jObject.Property(SchemaVersionProperty) != null, Is.True);
            Assert.That(jObject.Property(SchemaVersionProperty).Value.ToString(), Is.EqualTo(DefaultVersion));
        }

        [Test]
        public void AMigratorsOfUnknownTypeIsDefined_ObjectIsNotUpdated()
        {
            // Arrange
            IStorageContext context = new SqlServerStorageContext(ConnectionString);
            context.Configure.VersionProvider = oldVersionProvider;

            IStorageArea area = context.Area(EntityTableName);

            JObject newEntity = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            JObject inserted = area.Insert("content", newEntity);
            Assert.That(inserted.Property(SchemaVersionProperty) != null, Is.True);
            Assert.That(inserted.Property(SchemaVersionProperty).Value.ToString(), Is.EqualTo(OldVersion));
            context.Release(EntityTableName);

            context = new SqlServerStorageContext(ConnectionString);
            context.Configure.VersionProvider = currentVersionProvider;
            context.MigrationManager.Add(new AddAttributeForVersion3OfUnknownTypeMigrator("newAttribute", "attributeValue"));
            area = context.Area(EntityTableName);

            // Act
            IEnumerable<JObject> result = area.Get("content");

            // Assert
            Assert.That(result.Count(), Is.EqualTo(1));
            JObject jObject = result.First();
            Assert.That(jObject.Property("name") != null, Is.True);
            Assert.That(jObject.Property("name").Value.ToString(), Is.EqualTo("Potatoes"));
            Assert.That(jObject.Property("count") != null, Is.True);
            Assert.That(jObject.Property("count").Value.ToString(), Is.EqualTo("10"));
            Assert.That(jObject.Property("newAttribute") != null, Is.False);
            Assert.That(jObject.Property(SchemaVersionProperty) != null, Is.True);
            Assert.That(jObject.Property(SchemaVersionProperty).Value.ToString(), Is.EqualTo(DefaultVersion));
        }

        [Test]
        public void TwoMigratorsAreDefined_MultipleObjectsExist_AllObjectsAreUpdated()
        {
            // Arrange
            IStorageContext context = new SqlServerStorageContext(ConnectionString);
            context.Configure.VersionProvider = oldVersionProvider;
            IStorageArea area = context.Area(EntityTableName);

            // Entity 1 (version 2)
            JObject newEntity = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            JObject inserted = area.Insert("content", newEntity);
            var potatoesId = inserted[IdProperty];
            Assert.That(inserted.Property(SchemaVersionProperty) != null, Is.True);
            Assert.That(inserted.Property(SchemaVersionProperty).Value.ToString(), Is.EqualTo(OldVersion));

            // Entity 2 (version 2)
            newEntity = JObject.Parse("{ name: 'Tomatoes', count: 5 }");
            inserted = area.Insert("content", newEntity);
            var tomatoesId = inserted[IdProperty];
            Assert.That(inserted.Property(SchemaVersionProperty) != null, Is.True);
            Assert.That(inserted.Property(SchemaVersionProperty).Value.ToString(), Is.EqualTo(OldVersion));
            context.Release(EntityTableName);

            context = new SqlServerStorageContext(ConnectionString);
            context.Configure.VersionProvider = new TestVersionProvider() { Current = "3" };
            area = context.Area(EntityTableName);

            // Entity 3 (version 3)
            newEntity = JObject.Parse("{ name: 'Jalapeños', size: 20 }");
            inserted = area.Insert("content", newEntity);
            var jalapenosId = inserted[IdProperty];
            Assert.That(inserted.Property(SchemaVersionProperty) != null, Is.True);
            Assert.That(inserted.Property(SchemaVersionProperty).Value.ToString(), Is.EqualTo("3"));
            context.Release(EntityTableName);

            context = new SqlServerStorageContext(ConnectionString);
            context.Configure.VersionProvider = currentVersionProvider;
            context.MigrationManager.Add(new AddAttributeForVersion4Migrator("newAttribute", "attributeValue"));
            context.MigrationManager.Add(new RenameAttributeForVersion3Migrator("count", "size"));
            area = context.Area(EntityTableName);

            // Act
            IEnumerable<JObject> result = area.Get("content");

            // Assert
            Assert.That(result.Count(), Is.EqualTo(3));
            foreach (var entity in result)
            {
                var entityId = entity[IdProperty];
                if (entityId.Equals(potatoesId))
                {
                    Assert.That(entity.Property("name") != null, Is.True);
                    Assert.That(entity.Property("name").Value.ToString(), Is.EqualTo("Potatoes"));
                    Assert.That(entity.Property("count") != null, Is.False);
                    Assert.That(entity.Property("size") != null, Is.True);
                    Assert.That(entity.Property("size").Value.ToString(), Is.EqualTo("10"));

                }
                else if (entityId.Equals(tomatoesId))
                {
                    Assert.That(entity.Property("name") != null, Is.True);
                    Assert.That(entity.Property("name").Value.ToString(), Is.EqualTo("Tomatoes"));
                    Assert.That(entity.Property("count") != null, Is.False);
                    Assert.That(entity.Property("size") != null, Is.True);
                    Assert.That(entity.Property("size").Value.ToString(), Is.EqualTo("5"));
                }
                else if (entityId.Equals(jalapenosId))
                {
                    Assert.That(entity.Property("name") != null, Is.True);
                    Assert.That(entity.Property("name").Value.ToString(), Is.EqualTo("Jalapeños"));
                    Assert.That(entity.Property("size") != null, Is.True);
                    Assert.That(entity.Property("size").Value.ToString(), Is.EqualTo("20"));
                }
                else
                {
                    Assert.Fail("Unknown entity: " + entity);
                }
                Assert.That(entity.Property("newAttribute") != null, Is.True);
                Assert.That(entity.Property("newAttribute").Value.ToString(), Is.EqualTo("attributeValue"));
                Assert.That(entity.Property(SchemaVersionProperty) != null, Is.True);
                Assert.That(entity.Property(SchemaVersionProperty).Value.ToString(), Is.EqualTo(DefaultVersion));
            }
        }

        

        [DataMigratorAttribute("content", "4")]
        internal class AddAttributeForVersion4Migrator : IDataMigrator
        {
            public string AttributeName { get; set; }
            public string AttributeValue { get; set; }

            public AddAttributeForVersion4Migrator(string attributeName, string attributeValue)
            {
                AttributeName = attributeName;
                AttributeValue = attributeValue;
            }

            public JObject Up(JObject source)
            {
                JObject migrated = (JObject)source.DeepClone();
                migrated[AttributeName] = AttributeValue;
                return migrated;
            }

            public JObject Down(JObject source)
            {
                throw new NotSupportedException();
            }
        }

        [DataMigratorAttribute("content", "3")]
        internal class RenameAttributeForVersion3Migrator : IDataMigrator
        {
            public string OldName { get; set; }
            public string NewName { get; set; }

            public RenameAttributeForVersion3Migrator(string oldName, string newName)
            {
                OldName = oldName;
                NewName = newName;
            }

            public JObject Up(JObject source)
            {
                JObject migrated = (JObject)source.DeepClone();
                migrated[NewName] = migrated[OldName];
                migrated.Remove(OldName);
                return migrated;
            }

            public JObject Down(JObject source)
            {
                throw new NotSupportedException();
            }
        }

        [DataMigratorAttribute("unknown", "3")]
        internal class AddAttributeForVersion3OfUnknownTypeMigrator : IDataMigrator
        {
            public string Name { get; set; }
            public string Value { get; set; }

            public AddAttributeForVersion3OfUnknownTypeMigrator(string name, string value)
            {
                Name = name;
                Value = value;
            }

            public JObject Up(JObject source)
            {
                JObject migrated = (JObject)source.DeepClone();
                migrated[Name] = Value;
                return migrated;
            }

            public JObject Down(JObject source)
            {
                throw new NotSupportedException();
            }
        }


        internal class TestVersionProvider : IVersionProvider
        {
            public string Current { get; set; }

            public int Compare(string x, string y)
            {
                if (String.IsNullOrEmpty(x) && String.IsNullOrEmpty(y))
                {
                    return 0;
                }
                if (String.IsNullOrEmpty(x))
                {
                    return -1;
                }
                if (String.IsNullOrEmpty(y))
                {
                    return 1;
                }
                // ReSharper disable once StringCompareToIsCultureSpecific
                return x.CompareTo(y);
            }

        }

    }
}
