using System.Collections.Generic;

namespace DotJEM.Json.Storage.Migration.Collections
{
    public interface IDataMigratorCollection
    {
        IDataMigratorCollection Add(IDataMigrator instance);

    }
}