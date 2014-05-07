using NUnit.Framework;

namespace DotJEM.Json.Storage.Test
{
    [TestFixture]
    public class Class1
    {
        [Test]
        public void Test()
        {
            IStorageContext context = new SqlServerStorageContext("Data Source=.\\DEV;Initial Catalog=json;Integrated Security=True");
            ITableAdapter table = context.Table("Test");

            Assert.That(table.Exists, Is.False);

            table.CreateTable();
            Assert.That(table.Exists, Is.True);
        }
    }
}
