using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DotJEM.Json.Storage.Configuration
{
    public interface IStorageConfigurator
    {
        IStorageConfigurator MapField(JsonField field, string name);
        IStorageAreaConfigurator Area(string name = "content");
    }

    public interface IStorageConfiguration
    {
        IDictionary<JsonField, string> Fields { get; }
    }

    public class StorageConfiguration : IStorageConfiguration, IStorageConfigurator
    {
        private readonly IDictionary<JsonField, string> readonlyFields;
        private readonly IDictionary<JsonField, string> fields = new Dictionary<JsonField, string>();
        private readonly IDictionary<string, StorageAreaConfiguration> configurations = new Dictionary<string, StorageAreaConfiguration>();

        public StorageAreaConfiguration this[string key]
        {
            get { return configurations.ContainsKey(key) ? configurations[key] : (configurations[key] = new StorageAreaConfiguration()); }
        }

        public StorageConfiguration()
        {
            MapField(JsonField.Id, "_id");
            MapField(JsonField.Version, "_version");
            MapField(JsonField.ContentType, "_contentType");
            MapField(JsonField.Created, "_created");
            MapField(JsonField.Updated, "_updated");
            readonlyFields = new ReadOnlyDictionary<JsonField, string>(fields);
        }

        public IDictionary<JsonField, string> Fields
        {
            get { return readonlyFields; }
        }

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