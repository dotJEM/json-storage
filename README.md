dotJEM JSON Storage
===================

Small and simple storage for JSON objects in SQL Server.

The basic concept will be:

```C#
IStorageContext context = new SqlServerStorageContext("...");
ITableAdapter area = context.Area("Test");

JObject item = area.Insert("item", JObject.Parse("{ name: 'Potatoes' }")); //Normally you would recieve a JObject from a client.

//Query all...
JObject item2 = area.Get("item").First(); 

//Query by ID...
//      note that the field name here is configurable
//      so for easier C# access we can use: config.MapField(JsonField.Id, "Id");
//      and then do: area.Get("item", item.Id);
JObject item3 = area.Get("item", item['$id']); 

Assert.That(item, Is.EqualTo(item2));
Assert.That(item, Is.EqualTo(item3));
```

For indexing and Querying use a Reverse index, e.g: Lucene
For easy indexing JSON in lucene in C#, use: https://github.com/dotJEM/json-index

Disclaimer: Only if you must!
=============================

Any real document database is recommended over this, but if you find your self in a situation as I do, where you only have access to either SQL Server or a similar RDB as backend storage or the file system, then feel free to use this.
