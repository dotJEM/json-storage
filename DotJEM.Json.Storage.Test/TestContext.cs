using System;

namespace DotJEM.Json.Storage.Test
{
    public static class TestContext
    {
        public static string ConnectionString => Environment.GetEnvironmentVariable("appveyor_sqlconnection") ?? "Data Source=.\\DEV;Initial Catalog=json;Integrated Security=True";
    }
}