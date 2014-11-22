using System.Collections.Generic;
using System.Dynamic;
using DotJEM.Json.Storage.Util;

namespace DotJEM.Json.Storage.Queries
{
    public enum StorageField
    {
        Id,
        Version,
        ContentType,
        Created,
        Updated,
        Data
    }

    public enum HistoryField
    {
        Fid,
        Deleted
    }

    public interface ICommandFactory
    {
        string this[string key] { get; set; }
    }

    public class SqlServerCommandFactory : DynamicObject, ICommandFactory
    {
        private readonly dynamic self;
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

            vars.Add("id", StorageField.Id)
                .Add("version", StorageField.Version)
                .Add("type", StorageField.ContentType)
                .Add("created", StorageField.Created)
                .Add("updated", StorageField.Updated)
                .Add("data", StorageField.Data);

            vars.Add("fid", HistoryField.Fid)
                .Add("deleted", HistoryField.Deleted);

            vars.Add("tableName", table)
                .Add("historyTableName", "{tableName}.history")
                .Add("db", database)
                .Add("tableFullName", "[{db}].[dbo].[{tableName}]")
                .Add("historyTableFullName", "[{db}].[dbo].[{historyTableName}]");

            //TODO: Replace with a command builder pattern.
            self.Insert = vars.Format("INSERT INTO {tableFullName} ([{version}], [{type}], [{created}], [{updated}], [{data}]) OUTPUT INSERTED.* VALUES (1, @{type}, @{created}, @{updated}, @{data});");
            self.Update = vars.Format(
                "UPDATE {tableFullName} SET [{version}] = [{version}] + 1, [{type}] = @{type}, [{updated}] = @{updated}, [{data}] = @{data}"
                          + " OUTPUT"
                          + "   DELETED.[{id}] as [DELETED_{id}], DELETED.[{version}] as [DELETED_{version}], DELETED.[{type}] as [DELETED_{type}],"
                          + "   DELETED.[{created}] as [DELETED_{created}], DELETED.[{updated}] as [DELETED_{updated}], DELETED.[{data}] as [DELETED_{data}],"
                          + "   INSERTED.[{id}] as [INSERTED_{id}], INSERTED.[{version}] as [INSERTED_{version}], INSERTED.[{type}] as [INSERTED_{type}],"
                          + "   INSERTED.[{created}] as [INSERTED_{created}], INSERTED.[{updated}] as [INSERTED_{updated}], INSERTED.[{data}] as [INSERTED_{data}]"
                          + " WHERE [{id}] = @{id};");
            self.Delete = vars.Format("DELETE FROM {tableFullName} OUTPUT DELETED.* WHERE [{id}] = @{id};");

            //TODO: Requires paging!
            self.SelectAll = vars.Format("SELECT * FROM {tableFullName} ORDER BY [{created}];");
            self.SelectAllByContentType = vars.Format("SELECT * FROM {tableFullName} WHERE [{type}] = @{type} ORDER BY [{created}];");

            self.SelectSingle = vars.Format("SELECT * FROM {tableFullName} WHERE [{id}] = @{id} ORDER BY [{created}];");

            self.CreateTable = vars.Format(
                @"CREATE TABLE [dbo].[{tableName}] (
                          [{id}] [uniqueidentifier] NOT NULL,
                          [{version}] [int] NOT NULL,
                          [{type}] [varchar](256) NOT NULL,
                          [{created}] [datetime] NOT NULL,
                          [{updated}] [datetime] NULL,
                          [{data}] [varbinary](max) NOT NULL,
                          [RV] [rowversion] NOT NULL,
                          CONSTRAINT [PK_{tableName}] PRIMARY KEY NONCLUSTERED (
                            [Id] ASC
                          ) WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
                        ) ON [PRIMARY];

                        ALTER TABLE [dbo].[{tableName}] ADD  CONSTRAINT [DF_{tableName}_{id}]  DEFAULT (NEWSEQUENTIALID()) FOR [{id}];

                        CREATE CLUSTERED INDEX [IX_{tableName}_{type}] ON [dbo].[{tableName}] (
                          [{type}] ASC
                        ) WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY];");

            self.TableExists = vars.Format(
                @"SELECT TOP 1 COUNT(*) FROM INFORMATION_SCHEMA.TABLES
                               WHERE TABLE_SCHEMA = 'dbo'
                                 AND TABLE_NAME = '{tableName}'");


            // HISTORY STUFF
            self.InsertHistory = vars.Format("INSERT INTO {historyTableFullName} ([{fid}], [{version}], [{type}], [{deleted}], [{created}], [{updated}], [{data}])"
                                           + " VALUES (@{fid}, @{version}, @{type}, @{deleted}, @{created}, @{updated}, @{data});");
            //TODO: Requires paging!
            self.SelectHistoryFor = vars.Format("SELECT * FROM {historyTableFullName} WHERE [{fid}] = @{fid} ORDER BY [{version}] DESC;");
            self.SelectDeletedHistoryByContentType = vars.Format("SELECT * FROM {historyTableFullName} WHERE [{deleted}] = 1 AND [{type}] = @{type} ORDER BY [{version}];");

            self.SelectHistoryForFromDate = vars.Format("SELECT * FROM {historyTableFullName} WHERE [{fid}] = @{fid} AND [{updated}] >= @{updated} ORDER BY [{version}] DESC;");
            self.SelectDeletedHistoryByContentTypeFromDate = vars.Format("SELECT * FROM {historyTableFullName} WHERE [{deleted}] = 1 AND [{updated}] >= @{updated} AND [{type}] = @{type} ORDER BY [{version}];");

            self.CreateHistoryTable = vars.Format(
                @"CREATE TABLE [dbo].[{historyTableName}] (
                          [{id}] [uniqueidentifier] NOT NULL,
                          [{fid}] [uniqueidentifier] NOT NULL,
                          [{version}] [int] NOT NULL,
                          [{type}] [varchar](256) NOT NULL,
                          [{deleted}] [bit] NOT NULL,
                          [{created}] [datetime] NOT NULL,
                          [{updated}] [datetime] NULL,
                          [{data}] [varbinary](max) NOT NULL,
                          [RV] [rowversion] NOT NULL,
                          CONSTRAINT [PK_{historyTableName}] PRIMARY KEY NONCLUSTERED (
                            [Id] ASC
                          ) WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
                        ) ON [PRIMARY];

                        ALTER TABLE [dbo].[{historyTableName}] ADD  CONSTRAINT [DF_{historyTableName}_{id}]  DEFAULT (NEWSEQUENTIALID()) FOR [{id}];

                        ALTER TABLE [dbo].[{historyTableName}] ADD  CONSTRAINT [DF_{historyTableName}_{deleted}]  DEFAULT ((0)) FOR [{deleted}];

                        CREATE CLUSTERED INDEX [IX_{historyTableName}_{type}] ON [dbo].[{historyTableName}] (
                          [{type}] ASC
                        ) WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY];

                        ALTER TABLE [dbo].[{historyTableName}] WITH NOCHECK ADD  CONSTRAINT [FK_{historyTableName}_{tableName}] FOREIGN KEY([{fid}])
                              REFERENCES [dbo].[{historyTableName}] ([{id}]);

                        ALTER TABLE [dbo].[{historyTableName}] NOCHECK CONSTRAINT [FK_{historyTableName}_{tableName}];");

            self.HistoryTableExists = vars.Format(
                @"SELECT TOP 1 COUNT(*) FROM INFORMATION_SCHEMA.TABLES
                                            WHERE TABLE_SCHEMA = 'dbo'
                                                AND TABLE_NAME = '{historyTableName}'");

        }
    }
}
