using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;

namespace DotJEM.Json.Storage.Test
{
    public static class TestContext
    {
        public static string ConnectionString 
            => Environment.GetEnvironmentVariable("appveyor_sqlconnection") 
            ?? "Data Source=.\\DEV;Initial Catalog=json;Integrated Security=True;TrustServerCertificate=True";

        public static void DropArea(string area)
        {
            using (SqlConnection connection = new SqlConnection(TestContext.ConnectionString))
            {
                connection.Open();
                List<string> tables = LookupAreaTables(area, connection).ToList();
                foreach (string table in tables)
                {
                    using (SqlCommand command = new SqlCommand { Connection = connection })
                    {
                        command.CommandText = $"DROP TABLE [dbo].[{table}]";
                        command.ExecuteScalar();
                    }
                }

            }
        }

        public static IEnumerable<string> LookupAreaTables(string area, SqlConnection connection)
        {
            using (SqlCommand command = new SqlCommand { Connection = connection })
            {
                command.CommandText = $"SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND ( TABLE_NAME = '{area}' OR TABLE_NAME LIKE '{area}.%' )";
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    int col = reader.GetOrdinal("TABLE_NAME");
                    while (reader.Read())
                        yield return reader.GetString(col);
                }
            }
        }
    }
}