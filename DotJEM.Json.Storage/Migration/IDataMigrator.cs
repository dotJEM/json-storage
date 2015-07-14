using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Migration
{
    public interface IDataMigrator
    {
        JObject Migrate(JObject source);
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class DataMigratorAttribute : Attribute
    {
        public string Version { get; private set; }
        public string ContentType { get; private set; }

        public DataMigratorAttribute(string contentType, string version)
        {
            Version = version;
            ContentType = contentType;
        }

        public static DataMigratorAttribute GetAttribute(Type type)
        {
            return type
                .GetCustomAttributes(typeof(DataMigratorAttribute), false)
                .Single() as DataMigratorAttribute;
        }
    }


}