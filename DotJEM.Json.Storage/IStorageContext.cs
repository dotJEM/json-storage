using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DotJEM.Json.Storage.Adapter;
using DotJEM.Json.Storage.Configuration;
using DotJEM.Json.Storage.Migration;
using Microsoft.Data.SqlClient;

namespace DotJEM.Json.Storage
{
    public interface IStorageContext
    {
        IStorageConfigurator Configure { get; }
        IStorageMigrationManager MigrationManager { get; }

        IAreaInformationCollection AreaInfos { get; }
        IStorageArea Area(string name = "content");
        bool Release(string name = "content");

        IDataColumnSerializer Serializer { get; }

        IStorageConfiguration Configuration { get; }
    }

    public class SqlServerStorageContext : IStorageContext
    {
        private readonly string connectionString;
        private readonly Dictionary<string, IStorageArea> openAreas = new Dictionary<string, IStorageArea>();
        private readonly StorageMigrationManager manager;
        private readonly StorageConfiguration configuration;

        public IDataColumnSerializer Serializer { get; }
        public IStorageConfigurator Configure => configuration;
        public IStorageMigrationManager MigrationManager => manager;
        public IAreaInformationCollection AreaInfos => ScanForAreas();

        public IStorageConfiguration Configuration => configuration;

        internal StorageConfiguration SqlServerConfiguration => configuration;

        public SqlServerStorageContext(string connectionString, IDataColumnSerializer serializer = null)
        {
            this.connectionString = connectionString;

            Serializer = serializer ?? new BsonDataColumnSerializer();
            configuration = new StorageConfiguration();

            manager = new StorageMigrationManager(configuration);
        }

        public IStorageArea Area(string name = "content")
        {
            if (!openAreas.ContainsKey(name))
                return openAreas[name] = new SqlServerStorageArea(this, name, manager.Initialized());
            return openAreas[name];
        }

        public bool Release(string name = "content")
        {
            return openAreas.Remove(name);
        }

        internal SqlConnection Connection()
        {
            return new SqlConnection(connectionString);
        }

        internal SqlConnection OpenConnection()
        {
            SqlConnection conn = this.Connection();
            conn.Open();
            return conn;
        }

        private IAreaInformationCollection ScanForAreas()
        {
            using (SqlConnection conn = Connection())
            {
                conn.Open();
                using (SqlCommand command = new SqlCommand($"SELECT TABLE_CATALOG, TABLE_SCHEMA, TABLE_NAME FROM [{conn.Database}].INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE';", conn))
                {
                    //command.Parameters.Add(new SqlParameter("database", SqlDbType.VarChar)).Value = conn.Database;
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        //int catalogColumn = reader.GetOrdinal("TABLE_CATALOG");
                        //int schemaColumn = reader.GetOrdinal("TABLE_SCHEMA");
                        List<string> names = EmptyReader(reader, reader.GetOrdinal("TABLE_NAME")).ToList();
                        reader.Close();
                        return new AreaInformationCollection(names);
                    }
                }
            }

            IEnumerable<string> EmptyReader(SqlDataReader reader, int nameColumn)
            {
                while (reader.Read())
                    yield return reader.GetString(nameColumn);
            }
        }
    }

    public interface IAreaInformationCollection : IEnumerable<IAreaInformation>
    {
        bool TryGetValue(string key, out IAreaInformation value);
        IAreaInformation this[string key] { get; }
    }

    public class AreaInformationCollection : IAreaInformationCollection
    {
        private readonly Lazy<Dictionary<string, IAreaInformation>> infos;


        public AreaInformationCollection(IEnumerable<string> names)
        {
            names = names.ToList(); //note: Fource evaluation so that er load all items into memmory.
            infos = new Lazy<Dictionary<string, IAreaInformation>>(() => Build(names));
        }
        public bool TryGetValue(string key, out IAreaInformation value) => infos.Value.TryGetValue(key, out value);
        public IAreaInformation this[string key] => infos.Value[key];

        private Dictionary<string, IAreaInformation> Build(IEnumerable<string> names)
        {
            Queue<string> queue = new Queue<string>(names.OrderByDescending(name => name.Count(c => c == '.')));

            Dictionary<string, AreaInformation> areas = new Dictionary<string, AreaInformation>();



            //TABLE_CATALOG TABLE_SCHEMA    TABLE_NAME
            //NSW dbo settings
            //NSW dbo settings.seed
            //NSW dbo settings.changelog

            //NSW dbo content
            //NSW dbo content.seed
            //NSW dbo content.changelog
            //NSW dbo content.history

            //NSW dbo content.details
            //NSW dbo content.details.seed
            //NSW dbo content.details.changelog
            //NSW dbo content.details.history

            //NSW dbo diagnostic
            //NSW dbo diagnostic.seed
            //NSW dbo diagnostic.changelog

            //NSW dbo statistic
            //NSW dbo statistic.seed
            //NSW dbo statistic.changelog

            while (queue.Count > 0)
            {
                string name = queue.Dequeue();
                string[] parts = name.Split('.');

                if (!areas.ContainsKey(name))
                {
                    string areaName = string.Join(".", parts.Take(parts.Length - 1));
                    if (!areas.TryGetValue(areaName, out AreaInformation area))
                        areas.Add(areaName, area = new AreaInformation(areaName));
                    area.AddTable(name);
                }
            }

            return areas.ToDictionary(pair => pair.Key, pair => (IAreaInformation)pair.Value);
        }

        public IEnumerator<IAreaInformation> GetEnumerator() => infos.Value.Values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public interface IAreaInformation
    {
        string Name { get; }
        string SeedTableName { get; }
        string ChangeLogTableName { get; }
        string HistoryTableName { get; }

        bool HasHistory { get; }

        IEnumerable<string> Tables { get; }
    }

    public class AreaInformation : IAreaInformation
    {
        private readonly HashSet<string> tables = new HashSet<string>();

        public string Name { get; }

        public IEnumerable<string> Tables => tables;

        public string HistoryTableName => $"{Name}.history";
        public string ChangeLogTableName => $"{Name}.changelog";
        public string SeedTableName => $"{Name}.seed";

        public bool HasHistory => tables.Contains(HistoryTableName);

        public AreaInformation(string name)
        {
            this.Name = name;
            tables.Add(name);
        }

        public void AddTable(string name) => tables.Add(name);
    }
}