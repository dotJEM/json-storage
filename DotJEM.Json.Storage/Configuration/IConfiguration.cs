namespace DotJEM.Json.Storage.Configuration
{
    public interface IStorageAreaConfiguration
    {
        
    }

    public interface IFieldNames
    {
        string Id { get; set; }
        string Version { get; }
        string ContentType { get; set; }
        string Created { get; set; }
        string Updated { get; set; }
    }

    public interface IConfiguration
    {
        IFieldNames Fields { get; }
    }

    public class Configuration : IConfiguration, IFieldNames
    {
        public IFieldNames Fields
        {
            get { return this; }
        }

        public string Id { get; set; }
        public string ContentType { get; set; }
        public string Created { get; set; }
        public string Updated { get; set; }
        public string Version { get; set; }

        public Configuration()
        {
            Id = "_id";
            ContentType = "_contentType";
            Created = "_created";
            Updated = "_updated";
            Version = "_version";
        }
    }
}