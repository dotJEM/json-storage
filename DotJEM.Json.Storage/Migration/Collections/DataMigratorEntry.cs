namespace DotJEM.Json.Storage.Migration.Collections
{
    public class DataMigratorEntry
    {
        public string Version { get; private set; }
        public string ContentType { get; private set; }
        public IDataMigrator Migrator { get; private set; }

        public DataMigratorEntry(string contentType, string version, IDataMigrator migrator)
        {
            Version = version;
            ContentType = contentType;
            Migrator = migrator;
        }
    }
}