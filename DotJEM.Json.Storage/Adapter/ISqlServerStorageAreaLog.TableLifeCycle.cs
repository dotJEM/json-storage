using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace DotJEM.Json.Storage.Adapter;

public partial class SqlServerStorageAreaLog
{
    
    private void EnsureTable()
    {
        if (initialized)
            return;

        if (!TableExists)
            CreateTable();

        EnsureIndexes();

        initialized = true;
    }

    private void EnsureIndexes()
    {
        EnsureIndex($"{area.Name}.changelog.id_fid_index", "ChangeLogIdFidIndex");
        EnsureIndex($"{area.Name}.changelog.id_fid_action_index", "ChangeLogIdFidActionIndex");
        EnsureIndex($"{area.Name}.changelog.fid_id_index", "ChangeLogFidIdIndex");
    }

    private void EnsureIndex(string name, string commandName)
    {
        if (Indexes.ContainsKey(name))
            return;

        lock (padlock)
        {
            if (Indexes.ContainsKey(name))
                return;

            CreateIndex(commandName);
        }
    }

    private void CreateIndex(string commandName)
    {
        using SqlConnection connection = context.Connection();
        connection.Open();
        using SqlCommand command = new();
        command.Connection = connection;
        command.CommandText = area.Commands[commandName];
        command.ExecuteNonQuery();
    }

    private Dictionary<string, IndexDefinition> Indexes => indexes.Value;

    //TODO: Lazy evaliated result set: The lazy definition + load method could probably be mixed into a single construct making this easier in the future to manage other such constructs.
    private readonly Lazy<Dictionary<string, IndexDefinition>> indexes;

    private IEnumerable<IndexDefinition> LoadIndexes()
    {
        using SqlConnection connection = context.Connection();
        connection.Open();
        using SqlCommand command = new SqlCommand { Connection = connection };
        command.CommandText = area.Commands["ChangeLogIndexes"];
        using SqlDataReader reader = command.ExecuteReader();
        int nameColumn = reader.GetOrdinal("name");
        while (reader.Read())
        {
            string name = reader.GetString(nameColumn);
            yield return new IndexDefinition(name);
        }
    }

    private bool TableExists
    {
        get
        {
            using SqlConnection connection = context.Connection();
            connection.Open();
            using SqlCommand command = new SqlCommand { Connection = connection };
            command.CommandText = area.Commands["LogTableExists"];
            object result = command.ExecuteScalar();
            return 1 == Convert.ToInt32(result);
        }
    }

    private void CreateTable()
    {
        using SqlConnection connection = context.Connection();
        lock (padlock)
        {
            if (TableExists)
                return;

            connection.Open();
            using SqlCommand command = new();
            command.Connection = connection;
            command.CommandText = area.Commands["CreateLogTable"];
            command.ExecuteNonQuery();
        }
    }

    private class IndexDefinition
    {
        public string Name { get; }

        public IndexDefinition(string name)
        {
            Name = name;
        }

    }

}