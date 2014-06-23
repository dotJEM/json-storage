using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Configuration
{
    public interface IJObjectDecorator
    {
        JObject Decorate(JObject obj);
    }
}