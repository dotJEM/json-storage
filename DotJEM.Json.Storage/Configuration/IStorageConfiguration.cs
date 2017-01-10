using System.Collections.Generic;
using System.Collections.ObjectModel;
using DotJEM.Json.Storage.Migration;

namespace DotJEM.Json.Storage.Configuration
{
    public interface IStorageConfigurator
    {
        IStorageConfigurator MapField(JsonField field, string name);
        IStorageAreaConfigurator Area(string name = "content");
        IVersionProvider VersionProvider { get; set; }
        int ReadCommandTimeout { get; set; }
    }

    public interface IStorageConfiguration
    {
        IDictionary<JsonField, string> Fields { get; }
    }

    public class StorageConfiguration : IStorageConfiguration, IStorageConfigurator
    {
        private readonly IDictionary<JsonField, string> fields = new Dictionary<JsonField, string>();
        private readonly IDictionary<string, StorageAreaConfiguration> configurations = new Dictionary<string, StorageAreaConfiguration>();

        public StorageAreaConfiguration this[string key] => configurations.ContainsKey(key) ? configurations[key] : (configurations[key] = new StorageAreaConfiguration());

        public IVersionProvider VersionProvider { get; set; }

        public int ReadCommandTimeout { get; set; }

        public StorageConfiguration()
        {
            MapField(JsonField.Id, "$id");
            MapField(JsonField.Reference, "$reference");
            MapField(JsonField.Version, "$version");
            MapField(JsonField.ContentType, "$contentType");
            MapField(JsonField.Created, "$created");
            MapField(JsonField.Updated, "$updated");
            MapField(JsonField.Area, "$area");
            MapField(JsonField.SchemaVersion, "$schemaVersion");
            
            Fields = new ReadOnlyDictionary<JsonField, string>(fields);
            VersionProvider = new NullVersionProvider();
        }

        public IDictionary<JsonField, string> Fields { get; }

        public IStorageConfigurator MapField(JsonField field, string name)
        {
            fields[field] = name;
            return this;
        }

        public IStorageAreaConfigurator Area(string name = "content")
        {
            IStorageAreaConfiguration configuration = this[name];
            return configuration.Configurator;
        }

    }
}