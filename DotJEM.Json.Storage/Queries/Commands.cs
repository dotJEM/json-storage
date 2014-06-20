using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
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
        string Select(string contentType, ICollection<Guid> guids);
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
            self.Insert = vars.Format("INSERT INTO {from} ([{version}], [{type}], [{created}], [{data}]) OUTPUT INSERTED.* VALUES (1, @{type}, @{created}, @{data});");
            self.Update = vars.Format(
                "UPDATE {from} SET [{version}] = [{version}] + 1, [{type}] = @{type}, [{updated}] = @{updated}, [{data}] = @{data}"
                          + " OUTPUT"
                          + "   DELETED.[{id}] as [DELETED_{id}], DELETED.[{version}] as [DELETED_{version}], DELETED.[{type}] as [DELETED_{type}],"
                          + "   DELETED.[{created}] as [DELETED_{created}], DELETED.[{updated}] as [DELETED_{updated}], DELETED.[{data}] as [DELETED_{data}],"
                          + "   INSERTED.[{id}] as [INSERTED_{id}], INSERTED.[{version}] as [INSERTED_{version}], INSERTED.[{type}] as [INSERTED_{type}],"
                          + "   INSERTED.[{created}] as [INSERTED_{created}], INSERTED.[{updated}] as [INSERTED_{updated}], INSERTED.[{data}] as [INSERTED_{data}]"
                          + " WHERE [{id}] = @{id};");
            self.Delete = vars.Format("DELETE FROM {from} OUTPUT DELETED.* WHERE [{id}] = @{id};");

            self.SelectAll = vars.Format("SELECT * FROM {from} ORDER BY [{created}];");
            self.SelectAllByContentType = vars.Format("SELECT * FROM {from} WHERE [{type}] = @{type} ORDER BY [{created}];");
            self.SelectSingle = vars.Format("SELECT * FROM {from} WHERE [{id}] = @{id} ORDER BY [{created}];");
            self.SelectSingleByContentType = vars.Format("SELECT * FROM {from} WHERE [{id}] = @{id} AND [{type}] = @{type} ORDER BY [{created}];");
            self.SelectMultiple = vars.Format("SELECT * FROM {from} WHERE [{id}] IN ($IDS;) ORDER BY [{created}];");
            self.SelectMultipleByContentType = vars.Format("SELECT * FROM {from} WHERE [{type}] = @{type} AND [{id}] IN ($IDS;) ORDER BY [{created}];");

            self.CreateTable = vars.Format(
                @"CREATE TABLE [dbo].[{table}] (
                          [{id}] [uniqueidentifier] NOT NULL,
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

                        ALTER TABLE [dbo].[{table}] ADD  CONSTRAINT [DF_{table}_{id}]  DEFAULT (NEWSEQUENTIALID()) FOR [{id}];

                        CREATE CLUSTERED INDEX [IX_{table}_{type}] ON [dbo].[{table}] (
                          [{type}] ASC
                        ) WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY];");

            self.TableExists = vars.Format(
                @"SELECT TOP 1 COUNT(*) FROM INFORMATION_SCHEMA.TABLES
                               WHERE TABLE_SCHEMA = 'dbo'
                                 AND TABLE_NAME = '{table}'");
            
            self.CreateHistoryTable = vars.Format(
                @"CREATE TABLE [dbo].[{table}History] (
                          [{id}] [uniqueidentifier] NOT NULL,
                          [Fid] [uniqueidentifier] NOT NULL,
                          [{version}] [int] NOT NULL,
                          [{type}] [varchar](256) NOT NULL,
                          [{created}] [datetime] NOT NULL,
                          [{data}] [varbinary](max) NOT NULL,
                          [RV] [rowversion] NOT NULL,
                          CONSTRAINT [PK_{table}History] PRIMARY KEY NONCLUSTERED (
                            [Id] ASC
                          ) WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
                        ) ON [PRIMARY];

                        ALTER TABLE [dbo].[{table}History] ADD  CONSTRAINT [DF_{table}History_{id}]  DEFAULT (NEWSEQUENTIALID()) FOR [{id}];

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

        public string Select(string contentType, ICollection<Guid> guids)
        {
            if (!string.IsNullOrEmpty(contentType))
            {
                return InternalSelectWithContentType(guids);
            }

            switch (guids.Count)
            {
                case 0: return self.SelectAll;
                case 1: return self.SelectSingle;
                default: return self.SelectMultiple.Replace("$IDS;", IdsToString(guids));
            }
        }

        private string InternalSelectWithContentType(ICollection<Guid> guids)
        {
            switch (guids.Count)
            {
                case 0: return self.SelectAllByContentType;
                case 1: return self.SelectSingleByContentType;
                default: return self.SelectMultipleByContentType.Replace("$IDS;", IdsToString(guids));
            }
        }

        private static string IdsToString(IEnumerable<Guid> guids)
        {
            return string.Join(",", guids.Select(id => id.ToString()));
        }
    }
}
