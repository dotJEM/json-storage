using System.Collections.Generic;

namespace DotJEM.Json.Storage.Configuration
{
    public interface IStorageAreaConfiguration
    {
        bool HistoryEnabled { get; }
        IHistoryEnabledStorageArea EnableHistory();
    }

    public interface IHistoryEnabledStorageArea : IStorageAreaConfiguration
    {
        IHistoryEnabledStorageArea RegisterDecorator(IJObjectDecorator decorator);
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

        public IHistoryEnabledStorageArea RegisterDecorator<T>() where T : IJObjectDecorator, new()
        {
            return RegisterDecorator(new T());
        }

        public IHistoryEnabledStorageArea RegisterDecorator(IJObjectDecorator decorator)
        {
            decorators.Add(decorator);
            return this;
        }
    }
}