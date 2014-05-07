dotJEM JSON Storage
===================

Small and simple storage for JSON objects in SQL Server.

The basic concept will be:

```C#
IStorageContext context = new SqlServerStorageContext("...");
ITableAdapter area = context.Area("Test");

JObject item = area.Insert("item", JObject.Parse("{ name: 'Potatoes' }"));
JObject item2 = area.Get("item").First();

Assert.That(item, Is.EqualTo(item2));
```

For indexing and Querying use a Reverse index, e.g: https://github.com/dotJEM/json-index
