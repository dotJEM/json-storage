using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using DotJEM.Json.Storage.Linq;

namespace DotJEM.Json.Storage.Test.Linq
{
    [TestFixture]
    public class StorageAreaQueryableProviderTest
    {
        [Test]
        public void Provide()
        {
            StorageAreaQ storage = new StorageAreaQ(new SqlConnection("Data Source=.\\DEV;Initial Catalog=json;Integrated Security=True"));


            IQueryable<Entity> other = from entity in storage.Open("test")
                where entity.ContentType == "dummy"
                select entity;

            Entity[] list = other.ToArray();

        }

    }
}
