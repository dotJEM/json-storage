using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Configuration
{
    public interface IStorageAreaConfigurator
    {
        IHistoryEnabledStorageAreaConfigurator EnableHistory();

        IStorageAreaConfigurator UseChangeLogDataProvider(IChangeLogDataProvider differ);
    }

    public interface IStorageAreaConfiguration
    {
        IStorageAreaConfigurator Configurator { get; }

        bool HistoryEnabled { get; }
        bool UpdateOnMigrate { get; }

        IChangeLogDataProvider ChangeDataProvider { get; }
    }

    /// <summary>
    /// Provides a hook into additional data stored in the changelog.
    /// </summary>
    /// <remarks>
    /// This could be a diff of the <see cref="JObject"/> but the amount of data should be carefully considered.
    /// It could also simply be extra audit information, e.g. user performing the operation etc.
    /// </remarks>
    public interface IChangeLogDataProvider
    {
        JObject Diff(JObject prev, JObject next);
    }

    public class NullChangeLogDataProvider : IChangeLogDataProvider
    {
        public JObject Diff(JObject prev, JObject next) => new ();
    }

    public class FuncChangeLogDataProvider : IChangeLogDataProvider
    {
        private readonly Func<JObject, JObject, JObject> provider;

        public FuncChangeLogDataProvider(Func<JObject, JObject, JObject> provider)
        {
            this.provider = provider;
        }

        public JObject Diff(JObject prev, JObject next) => provider(prev,next);
    }

    public interface IHistoryEnabledStorageAreaConfigurator : IStorageAreaConfigurator
    {
        IHistoryEnabledStorageAreaConfiguration RegisterDecorator(IJObjectDecorator decorator);
        new IHistoryEnabledStorageAreaConfigurator UseDiffer(IChangeLogDataProvider differ);
    }

    public interface IHistoryEnabledStorageAreaConfiguration : IStorageAreaConfiguration
    {
        new IHistoryEnabledStorageAreaConfigurator Configurator { get; }

        IEnumerable<IJObjectDecorator> Decorators { get; }
    }
    public static class StorageAreaConfigurationExt
    {
        public static IStorageAreaConfigurator UseChangeLogDataProvider(this IStorageAreaConfigurator self, Func<JObject, JObject, JObject> provider)
            => self.UseChangeLogDataProvider(new FuncChangeLogDataProvider(provider));

        public static IHistoryEnabledStorageAreaConfigurator UseChangeLogDataProvider(this IHistoryEnabledStorageAreaConfigurator self, Func<JObject, JObject, JObject> provider)
            => self.UseDiffer(new FuncChangeLogDataProvider(provider));

    }
    public class StorageAreaConfiguration : IHistoryEnabledStorageAreaConfiguration, IHistoryEnabledStorageAreaConfigurator
    {
        private readonly List<IJObjectDecorator> decorators = new List<IJObjectDecorator>();

        IStorageAreaConfigurator IStorageAreaConfiguration.Configurator { get { return this; } }
        IHistoryEnabledStorageAreaConfigurator IHistoryEnabledStorageAreaConfiguration.Configurator { get { return this; } }

        public bool HistoryEnabled { get; private set; }
        public bool UpdateOnMigrate { get; private set; }
        public IChangeLogDataProvider ChangeDataProvider { get; private set; }

        public IEnumerable<IJObjectDecorator> Decorators { get { return decorators.AsReadOnly(); } }

        public IHistoryEnabledStorageAreaConfigurator EnableHistory()
        {
            HistoryEnabled = true;
            return this;
        }

        public IHistoryEnabledStorageAreaConfigurator UseDiffer(IChangeLogDataProvider differ)
        {
            ChangeDataProvider = differ;
            return this;
        }

        IStorageAreaConfigurator IStorageAreaConfigurator.UseChangeLogDataProvider(IChangeLogDataProvider differ)
        {
            return UseDiffer(differ);
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