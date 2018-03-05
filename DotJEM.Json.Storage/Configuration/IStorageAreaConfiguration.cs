using System.Collections.Generic;

namespace DotJEM.Json.Storage.Configuration
{
    public interface IStorageAreaConfigurator
    {
        IHistoryEnabledStorageAreaConfigurator EnableHistory();
    }

    public interface IStorageAreaConfiguration
    {
        IStorageAreaConfigurator Configurator { get; }

        bool HistoryEnabled { get; }
        bool UpdateOnMigrate { get; }
    }

    public interface IHistoryEnabledStorageAreaConfigurator : IStorageAreaConfigurator
    {
        IHistoryEnabledStorageAreaConfiguration RegisterDecorator(IJObjectDecorator decorator);
    }

    public interface IHistoryEnabledStorageAreaConfiguration : IStorageAreaConfiguration
    {
        new IHistoryEnabledStorageAreaConfigurator Configurator { get; }

        IEnumerable<IJObjectDecorator> Decorators { get; }
    }

    public class StorageAreaConfiguration : IHistoryEnabledStorageAreaConfiguration, IHistoryEnabledStorageAreaConfigurator
    {
        private readonly List<IJObjectDecorator> decorators = new List<IJObjectDecorator>();

        IStorageAreaConfigurator IStorageAreaConfiguration.Configurator { get { return this; } }
        IHistoryEnabledStorageAreaConfigurator IHistoryEnabledStorageAreaConfiguration.Configurator { get { return this; } }

        public bool HistoryEnabled { get; private set; }
        public bool UpdateOnMigrate { get; private set; }

        public IEnumerable<IJObjectDecorator> Decorators { get { return decorators.AsReadOnly(); } }

        public IHistoryEnabledStorageAreaConfigurator EnableHistory()
        {
            HistoryEnabled = true;
            return this;
        }

        public IHistoryEnabledStorageAreaConfigurator EnableUpdateOnMigrate()
        {
            UpdateOnMigrate = true;
            return this;
        }

        public IHistoryEnabledStorageAreaConfiguration RegisterDecorator<T>() where T : IJObjectDecorator, new()
        {
            return RegisterDecorator(new T());
        }

        public IHistoryEnabledStorageAreaConfiguration RegisterDecorator(IJObjectDecorator decorator)
        {
            decorators.Add(decorator);
            return this;
        }
    }
}