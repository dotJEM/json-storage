using System.Collections.Generic;

namespace DotJEM.Json.Storage.Migration.Collections
{
    internal class DataMigratorComparer : IComparer<DataMigratorEntry>
    {
        private readonly IVersionProvider versionProvider;

        internal DataMigratorComparer(IVersionProvider versionProvider)
        {
            this.versionProvider = versionProvider;
        }

        public int Compare(DataMigratorEntry left, DataMigratorEntry right)
        {
            return versionProvider.Compare(left.Version, right.Version);
        }
    }
}