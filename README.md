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

Disclaimer: Only if you must!
=============================

Any real document database is recommended over this, but if you find your self in a situation as I do, where you only have access to either SQL Server or a similar RDB as backend storage or the file system, then feel free to use this.
