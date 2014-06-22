using System.Collections.Generic;
using System.Collections.ObjectModel;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Configuration
{
    public interface IStorageAreaConfiguration
    {
        bool HistoryEnabled { get; }
        IHistoryEnabledStorageArea EnableHistory();
    }

    public interface IHistoryEnabledStorageArea : IStorageAreaConfiguration
    {
        IHistoryEnabledStorageArea RegisterDecorator(string name, IJObjectDecorator decorator);
    }

    public class StorageAreaConfiguration : IHistoryEnabledStorageArea
    {
        private readonly List<IJObjectDecorator> decorators = new List<IJObjectDecorator>();
        
        public bool HistoryEnabled { get; private set; }
        public IEnumerable<IJObjectDecorator> Decorators { get { return decorators.AsReadOnly(); } } 

        public IHistoryEnabledStorageArea EnableHistory()
        {
            HistoryEnabled = true;
            return this;
        }

        public IHistoryEnabledStorageArea RegisterDecorator<T>(string name) where T : IJObjectDecorator, new()
        {
            return RegisterDecorator(name, new T());
        }

        public IHistoryEnabledStorageArea RegisterDecorator(string name, IJObjectDecorator decorator)
        {
            decorators.Add(decorator);
            return this;
        }
    }

    public interface IJObjectDecorator
    {
        JObject Decorate(JObject obj);
    }

    public enum JsonField
    {
        Id,
        Version,
        ContentType,
        Created,
        Updated
    }

    public interface IStorageConfiguration
    {
        IDictionary<JsonField, string> Fields { get; }
        IStorageConfiguration MapField(JsonField field, string name);
        IStorageAreaConfiguration Area(string name);
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

        public IStorageAreaConfiguration Area(string name)
        {
            return configurations.ContainsKey(name) ? configurations[name] : (configurations[name] = new StorageAreaConfiguration());
        }
    }
}