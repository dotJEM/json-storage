using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using DotJEM.Json.Storage.Util;

namespace DotJEM.Json.Storage.Queries
{
    public interface IFields
    {
        string Id { get; }
        string ContentType { get; }
        string Created { get; }
        string Updated { get; }
        string Data { get; }
        string Version { get; }
    }

    public class DefaultFields : IFields
    {
        public string Id { get { return "Id"; } }
        public string ContentType { get { return "ContentType"; } }
        public string Created { get { return "Created"; } }
        public string Updated { get { return "Updated"; } }
        public string Data { get { return "Data"; } }
        public string Version { get { return "Version"; } }
    }

    public interface ICommandFactory
    {
        string this[string key] { get; set; }
        string Select(string contentType, ICollection<long> ids);
    }

    public class SqlServerCommandFactory : DynamicObject, ICommandFactory
    {
        private readonly dynamic self;
        private readonly IFields fields = new DefaultFields();
        private readonly Dictionary<string, object> commands = new Dictionary<string, object>();
        private readonly AdvPropertyBag vars = new AdvPropertyBag("{","}");

        public string this[string key]
        {
            get { return commands[key].ToString(); }
            set { commands[key] = value; }
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            commands.TryGetValue(binder.Name, out result);
            return true;
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            commands[binder.Name] = value;
            return true;
        }

        public SqlServerCommandFactory(string database, string table)
        {
            string tableFullName = string.Format("[{0}].[dbo].[{1}]", database, table);

            self = this;

            vars.Add("id", fields.Id)
                .Add("version", fields.Version)
                .Add("type", fields.ContentType)
                .Add("created", fields.Created)
                .Add("updated", fields.Updated)
                .Add("data", fields.Data);

            vars.Add("table", table)
                .Add("db", database)
                .Add("from", "[{db}].[dbo].[{table}]");

            //TODO: Replace with a command builder pattern.
            self.Update = vars.Format("UPDATE {from} SET [{version}] = [{version}] + 1, [{type}] = @{type}, [{updated}] = @{updated}, [{data}] = @{data} WHERE [{id}] = @{id};");
            self.Insert = vars.Format("INSERT INTO {from} ([{version}], [{type}], [{created}], [{data}]) VALUES (1, @{type}, @{created}, @{data}); SELECT SCOPE_IDENTITY();");
            self.Delete = vars.Format("DELETE FROM {from} WHERE [{id}] = @{id};");
            self.SelectAll = vars.Format("SELECT * FROM {from} ORDER BY [{created}];");
            self.SelectAllByContentType = vars.Format("SELECT * FROM {from} WHERE [{type}] = @{type} ORDER BY [{created}];");
            self.SelectSingle = vars.Format("SELECT * FROM {from} WHERE [{id}] = @{id} ORDER BY [{created}];");
            self.SelectSingleByContentType = vars.Format("SELECT * FROM {from} WHERE [{id}] = @{id} AND [{type}] = @{type} ORDER BY [{created}];");
            self.SelectMultiple = vars.Format("SELECT * FROM {from} WHERE [{id}] IN ($IDS;) ORDER BY [{created}];");
            self.SelectMultipleByContentType = vars.Format("SELECT * FROM {from} WHERE [{type}] = @{type} AND [{id}] IN ($IDS;) ORDER BY [{created}];");
            //self.Update = string.Format("UPDATE {0} SET [{1}] = @{1}, [{2}] = @{2}, [{3}] = @{3} WHERE [{4}] = @{4};",
            //    tableFullName, fields.ContentType, fields.Updated, fields.Data, fields.Id);

            //self.Insert = string.Format("INSERT INTO {0} ([{1}], [{2}], [{3}]) VALUES (@{1}, @{2}, @{3}); SELECT SCOPE_IDENTITY();",
            //    tableFullName, fields.ContentType, fields.Created, fields.Data);

            //self.Delete = string.Format("DELETE FROM {0} WHERE [{1}] = @{1};", tableFullName, fields.Id);
            //self.SelectAllByContentType = string.Format("SELECT * FROM {0} WHERE [{1}] = @{1} ORDER BY [{2}];",
            //    tableFullName, fields.ContentType, fields.Created);

            //self.SelectAll = string.Format("SELECT * FROM {0} ORDER BY [{1}];",
            //    tableFullName, fields.Created);

            //self.SelectSingleByContentType = string.Format("SELECT * FROM {0} WHERE [{1}] = @{1} AND [{2}] = @{2} ORDER BY [{3}];",
            //    tableFullName, fields.Id, fields.ContentType, fields.Created);

            //self.SelectSingle = string.Format("SELECT * FROM {0} WHERE [{1}] = @{1} ORDER BY [{2}];",
            //    tableFullName, fields.Id, fields.Created);

            //self.SelectMultipleByContentType = string.Format("SELECT * FROM {0} WHERE [{1}] = @{1} AND [{2}] IN ($IDS;) ORDER BY [{3}];",
            //    tableFullName, fields.ContentType, fields.Id, fields.Created);

            //self.SelectMultiple = string.Format("SELECT * FROM {0} WHERE [{1}] IN ($IDS;) ORDER BY [{2}];",
            //    tableFullName, fields.Id, fields.Created);

//            self.CreateTable = string.Format(
//                @"CREATE TABLE [dbo].[{0}] (
//                          [{1}] [bigint] IDENTITY(1,1) NOT NULL,
//                          [{2}] [int] NOT NULL,
//                          [{3}] [varchar](256) NOT NULL,
//                          [{4}] [datetime] NOT NULL,
//                          [{5}] [datetime] NULL,
//                          [{6}] [varbinary](max) NOT NULL,
//                          [RV] [rowversion] NOT NULL,
//                          CONSTRAINT [PK_{0}] PRIMARY KEY NONCLUSTERED (
//                            [Id] ASC
//                          ) WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
//                        ) ON [PRIMARY];
//
//                        CREATE CLUSTERED INDEX [IX_{0}_{2}] ON [dbo].[{0}] (
//                          [{2}] ASC
//                        ) WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY];"
//                , table, fields.Id, fields.Version, fields.ContentType, fields.Created, fields.Updated, fields.Data);

            self.CreateTable = vars.Format(
                @"CREATE TABLE [dbo].[{table}] (
                          [{id}] [bigint] IDENTITY(1,1) NOT NULL,
                          [{version}] [int] NOT NULL,
                          [{type}] [varchar](256) NOT NULL,
                          [{created}] [datetime] NOT NULL,
                          [{updated}] [datetime] NULL,
                          [{data}] [varbinary](max) NOT NULL,
                          [RV] [rowversion] NOT NULL,
                          CONSTRAINT [PK_{table}] PRIMARY KEY NONCLUSTERED (
                            [Id] ASC
                          ) WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
                        ) ON [PRIMARY];

                        CREATE CLUSTERED INDEX [IX_{table}_{type}] ON [dbo].[{table}] (
                          [{type}] ASC
                        ) WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY];");

            self.TableExists = vars.Format(
                @"SELECT TOP 1 COUNT(*) FROM INFORMATION_SCHEMA.TABLES
                               WHERE TABLE_SCHEMA = 'dbo'
                                 AND TABLE_NAME = '{table}'");
            
            self.CreateHistoryTable = vars.Format(
                @"CREATE TABLE [dbo].[{table}History] (
                          [{id}] [bigint] IDENTITY(1,1) NOT NULL,
                          [Fid] [bigint] NOT NULL,
                          [{version}] [int] NOT NULL,
                          [{type}] [varchar](256) NOT NULL,
                          [{created}] [datetime] NOT NULL,
                          [{data}] [varbinary](max) NOT NULL,
                          [RV] [rowversion] NOT NULL,
                          CONSTRAINT [PK_{table}History] PRIMARY KEY NONCLUSTERED (
                            [Id] ASC
                          ) WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
                        ) ON [PRIMARY];

                        CREATE CLUSTERED INDEX [IX_{table}History_{type}] ON [dbo].[{table}History] (
                          [{type}] ASC
                        ) WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY];

                        ALTER TABLE [dbo].[{table}History] WITH NOCHECK ADD  CONSTRAINT [FK_{table}History_{table}] FOREIGN KEY([Fid])
                              REFERENCES [dbo].[{table}History] ([{id}]);

                        ALTER TABLE [dbo].[{table}History] NOCHECK CONSTRAINT [FK_{table}History_{table}];");

            self.HistoryTableExists = vars.Format(
                @"SELECT TOP 1 COUNT(*) FROM INFORMATION_SCHEMA.TABLES
                                            WHERE TABLE_SCHEMA = 'dbo'
                                                AND TABLE_NAME = '{table}History'");

        }

        public string Select(string contentType, ICollection<long> ids)
        {
            if (!string.IsNullOrEmpty(contentType))
            {
                return InternalSelectWithContentType(ids);
            }

            switch (ids.Count)
            {
                case 0: return self.SelectAll;
                case 1: return self.SelectSingle;
                default: return self.SelectMultiple.Replace("$IDS;", IdsToString(ids));
            }
        }

        private string InternalSelectWithContentType(ICollection<long> ids)
        {
            switch (ids.Count)
            {
                case 0: return self.SelectAllByContentType;
                case 1: return self.SelectSingleByContentType;
                default: return self.SelectMultipleByContentType.Replace("$IDS;", IdsToString(ids));
            }
        }

        private static string IdsToString(IEnumerable<long> ids)
        {
            return string.Join(",", ids.Select(id => id.ToString(CultureInfo.InvariantCulture)));
        }
    }
}
