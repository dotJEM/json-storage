using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DotJEM.Json.Storage.Configuration
{
    public interface IStorageConfiguration
    {
        IDictionary<JsonField, string> Fields { get; }
        IStorageConfiguration MapField(JsonField field, string name);
        IStorageAreaConfiguration Area(string name = "Content");
    }

    public class StorageConfiguration : IStorageConfiguration
    {
        private readonly IDictionary<JsonField, string> readonlyFields;
        private readonly IDictionary<JsonField, string> fields = new Dictionary<JsonField, string>();
        private readonly IDictionary<string, IStorageAreaConfiguration> configurations = new Dictionary<string, IStorageAreaConfiguration>();

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

        public IStorageConfiguration MapField(JsonField field, string name)
        {
            fields[field] = name;
            return this;
        }

        public IStorageAreaConfiguration Area(string name = "Content")
        {
            return configurations.ContainsKey(name) ? configurations[name] : (configurations[name] = new StorageAreaConfiguration());
        }
    }
}