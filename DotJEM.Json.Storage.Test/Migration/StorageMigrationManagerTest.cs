using System;
using System.Collections.Generic;
using System.Linq;
using DotJEM.Json.Storage.Configuration;
using DotJEM.Json.Storage.Migration;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DotJEM.Json.Storage.Test.Migration
{
    [TestFixture]
    class StorageMigrationManagerTest
    {
        private static readonly string OldVersion2 = "2";
        private static readonly string OldVersion3 = "3";
        private static readonly string CurrentVersion = "5";
        private static readonly string FutureVersion = "5";

        private TestVersionProvider currentVersionProvider;
        private StorageConfiguration configuration;


        [SetUp]
        public void SetupTest()
        {
            currentVersionProvider = new TestVersionProvider { Current = CurrentVersion };
            configuration = new StorageConfiguration { VersionProvider = currentVersionProvider };
        }


        [Test]
        public void Upgrade_WhenNoMigratorsAreDefined_EntityIsNotUpdated()
        {
            // Arrange
            var manager = CreateStorageMigrationManager(Enumerable.Empty<IDataMigrator>());

            JObject entity = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            entity[configuration.Fields[JsonField.ContentType]] = "content";
            entity[configuration.Fields[JsonField.SchemaVersion]] = OldVersion2;
            JObject expectedEntity = (JObject)entity.DeepClone();
            expectedEntity[configuration.Fields[JsonField.SchemaVersion]] = CurrentVersion;

            // Act
            bool result = manager.Upgrade(ref entity);

            // Assert
            Assert.True(result);
            Assert.True(JToken.DeepEquals(entity, expectedEntity));
        }

        [Test]
        public void Upgrade_WhenNoContentMigratorsAreDefined_EntityIsNotUpdated()
        {
            // Arrange
            var manager = CreateStorageMigrationManager(new List<IDataMigrator> {
                new AddAttributeForVersion3OfUnknownTypeMigrator("newAttribute", "attributeValue")
            });

            JObject entity = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            entity[configuration.Fields[JsonField.ContentType]] = "content";
            entity[configuration.Fields[JsonField.SchemaVersion]] = OldVersion2;
            JObject expectedEntity = (JObject)entity.DeepClone();
            expectedEntity[configuration.Fields[JsonField.SchemaVersion]] = CurrentVersion;

            // Act
            bool result = manager.Upgrade(ref entity);

            // Assert
            Assert.True(result);
            Assert.True(JToken.DeepEquals(entity, expectedEntity));
        }

        [Test]
        public void Upgrade_WhenOneContentMigratorsIsDefined_EntityIsUpdated()
        {
            // Arrange
            var manager = CreateStorageMigrationManager(new List<IDataMigrator> {
                new AddAttributeForVersion4Migrator("newAttribute", "attributeValue")
            });

            JObject entity = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            entity[configuration.Fields[JsonField.ContentType]] = "content";
            entity[configuration.Fields[JsonField.SchemaVersion]] = OldVersion2;

            JObject expectedEntity = (JObject)entity.DeepClone();
            expectedEntity[configuration.Fields[JsonField.SchemaVersion]] = CurrentVersion;
            expectedEntity["newAttribute"] = "attributeValue";

            // Act
            bool result = manager.Upgrade(ref entity);

            // Assert
            Assert.True(result);
            Assert.True(JToken.DeepEquals(entity, expectedEntity));
        }

        [Test]
        public void Upgrade_WhenTwoContentMigratorsAreDefined_EntityIsUpdatedInOrder()
        {
            // Arrange
            var manager = CreateStorageMigrationManager(new List<IDataMigrator> {
                new RenameAttributeForVersion3Migrator("name", "newName"),
                new AddAttributeForVersion4Migrator("newAttribute", "attributeValue")
            });

            JObject entity = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            entity[configuration.Fields[JsonField.ContentType]] = "content";
            entity[configuration.Fields[JsonField.SchemaVersion]] = OldVersion2;

            JObject expectedEntity = JObject.Parse("{ newName: 'Potatoes', count: 10 }");
            expectedEntity[configuration.Fields[JsonField.ContentType]] = "content";
            expectedEntity[configuration.Fields[JsonField.SchemaVersion]] = CurrentVersion;
            expectedEntity["newAttribute"] = "attributeValue";

            // Act
            bool result = manager.Upgrade(ref entity);

            // Assert
            Assert.True(result);
            Assert.True(JToken.DeepEquals(entity, expectedEntity));
        }

        [Test]
        public void Upgrade_WhenEntityHasNoSchemaVersion_EntityIsUpdatedAccordingToVersionProvider()
        {
            // Arrange
            var manager = CreateStorageMigrationManager(new List<IDataMigrator> {
                new AddAttributeForVersion4Migrator("newAttribute", "attributeValue")
            });

            JObject entity = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            entity[configuration.Fields[JsonField.ContentType]] = "content";

            JObject expectedEntity = (JObject)entity.DeepClone();
            expectedEntity[configuration.Fields[JsonField.SchemaVersion]] = CurrentVersion;
            expectedEntity["newAttribute"] = "attributeValue";

            // Act
            bool result = manager.Upgrade(ref entity);

            // Assert
            Assert.True(result);
            Assert.True(JToken.DeepEquals(entity, expectedEntity));
        }

        [Test]
        public void Upgrade_WhenEntityHasCurrentSchemaVersion_EntityIsNotUpdated()
        {
            // Arrange
            var manager = CreateStorageMigrationManager(new List<IDataMigrator> {
                new AddAttributeForVersion4Migrator("newAttribute", "attributeValue")
            });

            JObject entity = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            entity[configuration.Fields[JsonField.ContentType]] = "content";
            entity[configuration.Fields[JsonField.SchemaVersion]] = CurrentVersion;

            JObject expectedEntity = (JObject)entity.DeepClone();

            // Act
            bool result = manager.Upgrade(ref entity);

            // Assert
            Assert.False(result);
            Assert.True(JToken.DeepEquals(entity, expectedEntity));
        }

        [Test]
        public void Upgrade_WhenEntityHasFutureSchemaVersion_EntityIsNotUpdated()
        {
            // Arrange
            var manager = CreateStorageMigrationManager(new List<IDataMigrator> {
                new AddAttributeForVersion4Migrator("newAttribute", "attributeValue")
            });

            JObject entity = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            entity[configuration.Fields[JsonField.ContentType]] = "content";
            entity[configuration.Fields[JsonField.SchemaVersion]] = FutureVersion;

            JObject expectedEntity = (JObject)entity.DeepClone();

            // Act
            bool result = manager.Upgrade(ref entity);

            // Assert
            Assert.False(result);
            Assert.True(JToken.DeepEquals(entity, expectedEntity));
        }

        [Test]
        public void Downgrade_WhenNoMigratorsAreDefined_EntityIsNotUpdated()
        {
            // Arrange
            var manager = CreateStorageMigrationManager(Enumerable.Empty<IDataMigrator>());

            JObject entity = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            entity[configuration.Fields[JsonField.ContentType]] = "content";
            entity[configuration.Fields[JsonField.SchemaVersion]] = CurrentVersion;
            JObject expectedEntity = (JObject)entity.DeepClone();
            expectedEntity[configuration.Fields[JsonField.SchemaVersion]] = OldVersion2;

            // Act
            bool result = manager.Downgrade(ref entity, OldVersion2);

            // Assert
            Assert.True(result);
            Assert.True(JToken.DeepEquals(entity, expectedEntity));
        }

        [Test]
        public void Downgrade_WhenNoContentMigratorsAreDefined_EntityIsNotUpdated()
        {
            // Arrange
            var manager = CreateStorageMigrationManager(new List<IDataMigrator> {
                new AddAttributeForVersion3OfUnknownTypeMigrator("newAttribute", "attributeValue")
            });

            JObject entity = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            entity[configuration.Fields[JsonField.ContentType]] = "content";
            entity[configuration.Fields[JsonField.SchemaVersion]] = CurrentVersion;
            JObject expectedEntity = (JObject)entity.DeepClone();
            expectedEntity[configuration.Fields[JsonField.SchemaVersion]] = OldVersion2;

            // Act
            bool result = manager.Downgrade(ref entity, OldVersion2);

            // Assert
            Assert.True(result);
            Assert.True(JToken.DeepEquals(entity, expectedEntity));
        }

        [Test]
        public void Downgrade_WhenOneContentMigratorsIsDefined_EntityIsUpdated()
        {
            // Arrange
            var manager = CreateStorageMigrationManager(new List<IDataMigrator> {
                new AddAttributeForVersion4Migrator("newAttribute", "attributeValue")
            });

            JObject entity = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            entity[configuration.Fields[JsonField.ContentType]] = "content";
            entity[configuration.Fields[JsonField.SchemaVersion]] = CurrentVersion;
            entity["newAttribute"] = "attributeValue";

            JObject expectedEntity = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            expectedEntity[configuration.Fields[JsonField.ContentType]] = "content";
            expectedEntity[configuration.Fields[JsonField.SchemaVersion]] = OldVersion2;
            expectedEntity["newAttribute_saved"] = "attributeValue";

            // Act
            bool result = manager.Downgrade(ref entity, OldVersion2);

            // Assert
            Assert.True(result);
            Assert.True(JToken.DeepEquals(entity, expectedEntity));
        }

        [Test]
        public void Downgrade_WhenTwoContentMigratorsAreDefined_EntityIsUpdatedInOrder()
        {
            // Arrange
            var manager = CreateStorageMigrationManager(new List<IDataMigrator> {
                new RenameAttributeForVersion3Migrator("name", "newName"),
                new AddAttributeForVersion4Migrator("newAttribute", "attributeValue")
            });

            JObject entity = JObject.Parse("{ newName: 'Potatoes', count: 10 }");
            entity[configuration.Fields[JsonField.ContentType]] = "content";
            entity[configuration.Fields[JsonField.SchemaVersion]] = CurrentVersion;
            entity["newAttribute"] = "updatedAttributeValue";

            JObject expectedEntity = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            expectedEntity[configuration.Fields[JsonField.ContentType]] = "content";
            expectedEntity[configuration.Fields[JsonField.SchemaVersion]] = OldVersion2;
            expectedEntity["newAttribute_saved"] = "updatedAttributeValue";

            // Act
            bool result = manager.Downgrade(ref entity, OldVersion2);

            // Assert
            Assert.True(result);
            Assert.True(JToken.DeepEquals(entity, expectedEntity));
        }

        [Test]
        public void Downgrade_WhenEntityHasNoSchemaVersion_EntityIsUpdated()
        {
            // Arrange
            var manager = CreateStorageMigrationManager(new List<IDataMigrator> {
                new AddAttributeForVersion4Migrator("newAttribute", "attributeValue")
            });

            JObject entity = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            entity[configuration.Fields[JsonField.ContentType]] = "content";

            JObject expectedEntity = (JObject)entity.DeepClone();

            // Act
            bool result = manager.Downgrade(ref entity, OldVersion2);

            // Assert
            Assert.False(result);
            Assert.True(JToken.DeepEquals(entity, expectedEntity));
        }

        [Test]
        public void Downgrade_WhenEntityHasTargetSchemaVersion_EntityIsNotUpdated()
        {
            // Arrange
            var manager = CreateStorageMigrationManager(new List<IDataMigrator> {
                new AddAttributeForVersion4Migrator("newAttribute", "attributeValue")
            });

            JObject entity = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            entity[configuration.Fields[JsonField.ContentType]] = "content";
            entity[configuration.Fields[JsonField.SchemaVersion]] = OldVersion2;

            JObject expectedEntity = (JObject)entity.DeepClone();

            // Act
            bool result = manager.Downgrade(ref entity, OldVersion2);

            // Assert
            Assert.False(result);
            Assert.True(JToken.DeepEquals(entity, expectedEntity));
        }

        [Test]
        public void Downgrade_WhenEntityHasOlderSchemaVersion_EntityIsNotUpdated()
        {
            // Arrange
            var manager = CreateStorageMigrationManager(new List<IDataMigrator> {
                new AddAttributeForVersion4Migrator("newAttribute", "attributeValue")
            });

            JObject entity = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            entity[configuration.Fields[JsonField.ContentType]] = "content";
            entity[configuration.Fields[JsonField.SchemaVersion]] = OldVersion2;

            JObject expectedEntity = (JObject)entity.DeepClone();

            // Act
            bool result = manager.Downgrade(ref entity, OldVersion3); // 2 older than 3

            // Assert
            Assert.False(result);
            Assert.True(JToken.DeepEquals(entity, expectedEntity));
        }

        [Test]
        public void Downgrade_WhenEntityHasNewerButStillOldSchemaVersion_EntityIsUpdated()
        {
            // Arrange
            var manager = CreateStorageMigrationManager(new List<IDataMigrator> {
                new RenameAttributeForVersion3Migrator("name", "newName"),
                new AddAttributeForVersion4Migrator("newAttribute", "attributeValue")
            });

            JObject entity = JObject.Parse("{ newName: 'Potatoes', count: 10 }");
            entity[configuration.Fields[JsonField.ContentType]] = "content";
            entity[configuration.Fields[JsonField.SchemaVersion]] = OldVersion3;

            JObject expectedEntity = JObject.Parse("{ name: 'Potatoes', count: 10 }");
            expectedEntity[configuration.Fields[JsonField.ContentType]] = "content";
            expectedEntity[configuration.Fields[JsonField.SchemaVersion]] = OldVersion2;

            // Act
            bool result = manager.Downgrade(ref entity, OldVersion2);

            // Assert
            Assert.True(result);
            Assert.True(JToken.DeepEquals(entity, expectedEntity));
        }


        // ------------------------------
        // Supporting classes and methods
        // ------------------------------

        private StorageMigrationManager CreateStorageMigrationManager(IEnumerable<IDataMigrator> migrators)
        {
            return new StorageMigrationManager(configuration, migrators).Initialized();
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
                JObject migrated = (JObject)source.DeepClone();
                migrated[AttributeName + "_saved"] = migrated[AttributeName];
                migrated.Remove(AttributeName);
                return migrated;
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
                JObject migrated = (JObject)source.DeepClone();
                migrated[OldName] = migrated[NewName];
                migrated.Remove(NewName);
                return migrated;
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
                throw new Exception("Upgrade of unknown migrator called");
            }

            public JObject Down(JObject source)
            {
                throw new Exception("Downgrade of unknown migrator called");
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
