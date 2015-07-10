using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Migration
{
    public interface IDataMigrator
    {
        string Version();
        JObject Migrate(JObject source);
    }
}
