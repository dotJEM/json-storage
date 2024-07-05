using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using DotJEM.Json.Storage.Util;

namespace DotJEM.Json.Storage.Queries
{
    public enum StorageField
    {
        Id,
        Fid,
        Reference,
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

    public enum LogField
    {
        Action,
        Changes
    }

    public interface ICommandFactory
    {
        string this[string key] { get; set; }
    }

    public class SqlServerCommandFactory : ICommandFactory
    {
        private readonly Dictionary<string, object> commands = new Dictionary<string, object>();

        public string this[string key]
        {
            get { return commands[key].ToString(); }
            set { commands[key] = value; }
        }

        public AdvPropertyBag Vars { get; } = new AdvPropertyBag("{", "}");


        public SqlServerCommandFactory(string database, string table)
        {
            //TODO: Now er can use C#'s new interpolation feature.
            Vars.Add("id", StorageField.Id)
                .Add("ref", StorageField.Reference)
                .Add("version", StorageField.Version)
                .Add("type", StorageField.ContentType)
                .Add("created", StorageField.Created)
                .Add("updated", StorageField.Updated)
                .Add("data", StorageField.Data);

            Vars.Add("fid", HistoryField.Fid)
                .Add("deleted", HistoryField.Deleted);

            Vars.Add("action", LogField.Action);

            Vars.Add("tableName", table)
                .Add("historyTableName", "{tableName}.history")
                .Add("seedTableName", "{tableName}.seed")
                .Add("logTableName", "{tableName}.changelog")
                .Add("db", database)
                .Add("tableFullName", "[{db}].[dbo].[{tableName}]")
                .Add("historyTableFullName", "[{db}].[dbo].[{historyTableName}]")
                .Add("seedTableFullName", "[{db}].[dbo].[{seedTableName}]")
                .Add("logTableFullName", "[{db}].[dbo].[{logTableName}]");


            //TODO: Replace with a command builder pattern.
            this["Insert"] = Vars.Format(
                "INSERT INTO {tableFullName} ( [{version}], [{type}], [{created}], [{updated}], [{data}], [{ref}] ) "
                + "OUTPUT INSERTED.* "
                + "SELECT [next_{version}], [next_{type}], [next_{created}], [next_{updated}], [next_{data}], [next_{ref}] "
                + "    FROM ( MERGE {seedTableFullName} WITH (HOLDLOCK) AS seedtable "
                + "           USING (SELECT @{type} as [{type}]) AS next_ref ON seedtable.[{type}] = next_ref.[{type}] "
                + "           WHEN MATCHED THEN        UPDATE SET seedtable.Seed = seedtable.Seed + 1 "
                + "           WHEN NOT MATCHED THEN    INSERT ([{type}]) VALUES (@{type}) "
                + "           OUTPUT 1, @{type}, @{created}, @{updated}, @{data}, INSERTED.Seed as [{ref}] ) "
                + "        as TEMP( [next_{version}], [next_{type}], [next_{created}], [next_{updated}], [next_{data}], [next_{ref}] );");

            this["Update"] = Vars.Format(
                "UPDATE {tableFullName} SET [{version}] = [{version}] + 1, [{updated}] = @{updated}, [{data}] = @{data}"
                          + " OUTPUT"
                          + "   DELETED.[{id}] as [DELETED_{id}], DELETED.[{ref}] as [DELETED_{ref}], DELETED.[{version}] as [DELETED_{version}], "
                          + "   DELETED.[{type}] as [DELETED_{type}], DELETED.[{created}] as [DELETED_{created}], "
                          + "   DELETED.[{updated}] as [DELETED_{updated}], DELETED.[{data}] as [DELETED_{data}],"
                          + "   INSERTED.[{id}] as [INSERTED_{id}], INSERTED.[{ref}] as [INSERTED_{ref}], INSERTED.[{version}] as [INSERTED_{version}], "
                          + "   INSERTED.[{type}] as [INSERTED_{type}], INSERTED.[{created}] as [INSERTED_{created}], "
                          + "   INSERTED.[{updated}] as [INSERTED_{updated}], INSERTED.[{data}] as [INSERTED_{data}]"
                          + " WHERE [{id}] = @{id};");
            this["Delete"] = Vars.Format("DELETE FROM {tableFullName} OUTPUT DELETED.* WHERE [{id}] = @{id};");

            //TODO: Requires paging!
            this["Count"] = Vars.Format("SELECT COUNT_BIG([{id}]) FROM {tableFullName};");
            this["CountByContentType"] = Vars.Format("SELECT COUNT_BIG([{id}]) FROM {tableFullName} WHERE [{type}] = @{type};");

            this["SelectAll"] = Vars.Format("SELECT * FROM {tableFullName} ORDER BY [{created}];");
            //TODO: This requires a better aproach as it puts responsibility of constructing queries outside of the Commands class (which is a horrible class anyways).
            this["SelectAllByGuids"] = Vars.Format("SELECT * FROM {tableFullName} WHERE [{id}] IN ( $$$INS$$$ ) ORDER BY [{created}];");

            this["SelectAllByContentType"] = Vars.Format("SELECT * FROM {tableFullName} WHERE [{type}] = @{type} ORDER BY [{created}];");

            this["SelectSingle"] = Vars.Format("SELECT * FROM {tableFullName} WHERE [{id}] = @{id} ORDER BY [{created}];");

            this["SelectAllPaged"] = Vars.Format(@"
                SELECT  *
                FROM    ( SELECT    ROW_NUMBER() OVER ( ORDER BY [{created}] ) AS rn, *
                          FROM      {tableFullName}
                        ) AS PagedResult
                WHERE   rn >= @rowstart
                    AND rn < @rowend
            ");


            this["SelectAllPagedByContentType"] = Vars.Format(@"
                SELECT  *
                FROM    ( SELECT    ROW_NUMBER() OVER ( ORDER BY [{created}] ) AS rn, *
                          FROM      {tableFullName}
                          WHERE [{type}] = @{type}
                        ) AS PagedResult
                WHERE   rn >= @rowstart
                    AND rn < @rowend
            ");

            this["CreateTable"] = Vars.Format(
                @"CREATE TABLE [dbo].[{tableName}] (
                          [{id}] [uniqueidentifier] NOT NULL,
                          [{ref}] [bigint] NOT NULL,
                          [{version}] [int] NOT NULL,
                          [{type}] [varchar](256) NOT NULL,
                          [{created}] [datetime] NOT NULL,
                          [{updated}] [datetime] NOT NULL,
                          [{data}] [varbinary](max) NOT NULL,
                          [RV] [rowversion] NOT NULL,
                          CONSTRAINT [PK_{tableName}] PRIMARY KEY CLUSTERED (
                            [{id}] ASC
                          ) WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
                        ) ON [PRIMARY];

                        ALTER TABLE [dbo].[{tableName}] ADD  CONSTRAINT [DF_{tableName}_{id}]  DEFAULT (NEWSEQUENTIALID()) FOR [{id}];");

            this["TableExists"] = Vars.Format(
                @"SELECT TOP 1 COUNT(*) FROM INFORMATION_SCHEMA.TABLES
                               WHERE TABLE_SCHEMA = 'dbo'
                                 AND TABLE_NAME = '{tableName}'");


            // HISTORY STUFF
            this["InsertHistory"] = Vars.Format("INSERT INTO {historyTableFullName} ([{fid}], [{ref}], [{version}], [{type}], [{deleted}], [{created}], [{updated}], [{data}])"
                                           + " VALUES (@{fid}, @{ref}, @{version}, @{type}, @{deleted}, @{created}, @{updated}, @{data});");
            //TODO: Requires paging!
            this["SelectHistoryFor"] = Vars.Format("SELECT * FROM {historyTableFullName} WHERE [{fid}] = @{fid} ORDER BY [{version}] DESC;");
            this["SelectDeletedHistoryByContentType"] = Vars.Format("SELECT * FROM {historyTableFullName} WHERE [{deleted}] = 1 AND [{type}] = @{type} ORDER BY [{version}];");

            this["SelectHistoryForByVersion"] = Vars.Format("SELECT * FROM {historyTableFullName} WHERE [{fid}] = @{fid} AND [{version}] = @{version}");
            this["SelectHistoryForFromDate"] = Vars.Format("SELECT * FROM {historyTableFullName} WHERE [{fid}] = @{fid} AND [{updated}] >= @{updated} ORDER BY [{version}] DESC;");
            this["SelectHistoryForToDate"] = Vars.Format("SELECT * FROM {historyTableFullName} WHERE [{fid}] = @{fid} AND [{updated}] <= @{updated} ORDER BY [{version}] DESC;");
            this["SelectHistoryForBetweenDate"] = Vars.Format("SELECT * FROM {historyTableFullName} WHERE [{fid}] = @{fid} AND [{updated}] >= @fromdate AND [{updated}] <= @todate ORDER BY [{version}] DESC;");
            this["SelectDeletedHistoryByContentTypeFromDate"] = Vars.Format("SELECT * FROM {historyTableFullName} WHERE [{deleted}] = 1 AND [{updated}] >= @{updated} AND [{type}] = @{type} ORDER BY [{version}];");
            this["SelectDeletedHistoryByContentTypeToDate"] = Vars.Format("SELECT * FROM {historyTableFullName} WHERE [{deleted}] = 1 AND [{updated}] <= @{updated} AND [{type}] = @{type} ORDER BY [{version}];");
            this["SelectDeletedHistoryByContentTypeBetweenDate"] = Vars.Format("SELECT * FROM {historyTableFullName} WHERE [{deleted}] = 1 AND [{updated}] >= @fromdate AND [{updated}] <= @todate AND [{type}] = @{type} ORDER BY [{version}];");

            //this["Delete"] = Vars.Format("DELETE FROM {tableFullName} OUTPUT DELETED.* WHERE [{id}] = @{id};");

            this["DeleteHistoryByDate"] = Vars.Format("DELETE FROM [dbo].[{historyTableName}] WHERE [{updated}] < @{updated};");

            this["CreateHistoryTable"] = Vars.Format(
                @"CREATE TABLE [dbo].[{historyTableName}] (
                          [{id}] [uniqueidentifier] NOT NULL,
                          [{fid}] [uniqueidentifier] NOT NULL,
                          [{ref}] [bigint] NOT NULL,
                          [{version}] [int] NOT NULL,
                          [{type}] [varchar](256) NOT NULL,
                          [{deleted}] [bit] NOT NULL,
                          [{created}] [datetime] NOT NULL,
                          [{updated}] [datetime] NOT NULL,
                          [{data}] [varbinary](max) NOT NULL,
                          [RV] [rowversion] NOT NULL,
                          CONSTRAINT [PK_{historyTableName}] PRIMARY KEY CLUSTERED (
                            [Id] ASC
                          ) WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
                        ) ON [PRIMARY];

                        ALTER TABLE [dbo].[{historyTableName}] ADD  CONSTRAINT [DF_{historyTableName}_{id}]  DEFAULT (NEWSEQUENTIALID()) FOR [{id}];

                        ALTER TABLE [dbo].[{historyTableName}] ADD  CONSTRAINT [DF_{historyTableName}_{deleted}]  DEFAULT ((0)) FOR [{deleted}];

                        ALTER TABLE [dbo].[{historyTableName}] WITH NOCHECK ADD  CONSTRAINT [FK_{historyTableName}_{tableName}] FOREIGN KEY([{fid}])
                              REFERENCES [dbo].[{historyTableName}] ([{id}]);

                        ALTER TABLE [dbo].[{historyTableName}] NOCHECK CONSTRAINT [FK_{historyTableName}_{tableName}];");

            this["HistoryTableExists"] = Vars.Format(
                @"SELECT TOP 1 COUNT(*) FROM INFORMATION_SCHEMA.TABLES
                                            WHERE TABLE_SCHEMA = 'dbo'
                                                AND TABLE_NAME = '{historyTableName}'");

            // SEED STUFF
            this["CreateSeedTable"] = Vars.Format(
                @"CREATE TABLE [dbo].[{seedTableName}](
	                        [{id}] [uniqueidentifier] NOT NULL CONSTRAINT [DF_{seedTableName}_Id] DEFAULT (newid()),
	                        [{type}] [varchar](256) NOT NULL,
	                        [Seed] [bigint] NOT NULL CONSTRAINT [DF_{seedTableName}_Seed] DEFAULT ((1)),
                            CONSTRAINT [PK_{seedTableName}] PRIMARY KEY NONCLUSTERED 
                            (
	                            [{id}] ASC
                            ) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY],

                            CONSTRAINT [IX_{seedTableName}.{type}] UNIQUE CLUSTERED 
                            (
	                            [{type}] ASC
                            ) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
                        ) ON [PRIMARY]");

            this["SeedTableExists"] = Vars.Format(
                @"SELECT TOP 1 COUNT(*) FROM INFORMATION_SCHEMA.TABLES
                               WHERE TABLE_SCHEMA = 'dbo'
                                 AND TABLE_NAME = '{seedTableName}'");

            // logTableName
            // logTableFullName

            // CHANGE LOG
            this["CreateLogTable"] = Vars.Format(
                @"CREATE TABLE [dbo].[{logTableName}] (
                          [{id}] [bigint] IDENTITY(1,1) NOT NULL,
                          [{fid}] [uniqueidentifier] NULL,
                          [{action}] [varchar](8) NOT NULL,
                          [{data}] [varbinary](max) NOT NULL,
                          [RV] [rowversion] NOT NULL,
                          CONSTRAINT [PK_{logTableName}] PRIMARY KEY CLUSTERED (
                            [Id] ASC
                          ) WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
                        ) ON [PRIMARY];");


            this["LogTableExists"] = Vars.Format(
                @"SELECT TOP 1 COUNT(*) FROM INFORMATION_SCHEMA.TABLES
                               WHERE TABLE_SCHEMA = 'dbo'
                                 AND TABLE_NAME = '{logTableName}'");

            this["SelectChanges"] = Vars.Format("SELECT * FROM {logTableFullName} WHERE [{id}] > @token;");
            this["SelectMaxGeneration"] = Vars.Format("SELECT MAX([{id}]) FROM {logTableFullName}");

            this["SelectChangesPureById"] = Vars.Format(@"
                SELECT TOP (@count)
	                {logTableFullName}.Id,
	                {logTableFullName}.[{fid}], 
	                {logTableFullName}.Action, 
	                {logTableFullName}.Data
                FROM ( 
	                FROM {logTableFullName}
	                WHERE [{id}] > @token AND [{fid}] = @fid
                ORDER BY Token
            ");

            this["SelectChangesWithDeletes"] = Vars.Format(@"
                SELECT TOP (@count)
	                {tableFullName}.Id,
	                {tableFullName}.Reference, 
	                {tableFullName}.Version, 
	                {tableFullName}.ContentType, 
	                {tableFullName}.Created, 
	                {tableFullName}.Updated, 
	                {tableFullName}.Data, 

	                changelogdata.[Action] AS [Action],
	                changelog.[Token],
	                changelog.[{fid}],
                    changelogdata.[Data] as Diff

                FROM ( 
	                SELECT MAX([{id}]) as Token, [{fid}] 
	                FROM {logTableFullName}
	                WHERE [{id}] > @token
	                GROUP BY [{fid}] ) changelog

	                JOIN {logTableFullName} changelogdata ON changelogdata.[{id}] = changelog.Token
	                LEFT JOIN {tableFullName} ON {tableFullName}.[{id}] = changelog.[{fid}]
                ORDER BY Token
            ");

            this["SelectChangesNoDeletes"] = Vars.Format(@"
                SELECT TOP (@count)
	                {tableFullName}.Id,
	                {tableFullName}.Reference, 
	                {tableFullName}.Version, 
	                {tableFullName}.ContentType, 
	                {tableFullName}.Created, 
	                {tableFullName}.Updated, 
	                {tableFullName}.Data, 

	                changelogdata.[Action] AS [Action],
	                changelog.[Token],
	                changelog.[{fid}],
                    changelogdata.[Data] as Diff

                FROM ( 
	                SELECT MAX([{id}]) as Token, [{fid}] 
	                FROM {logTableFullName}
	                WHERE [{id}] > @token
	                GROUP BY [{fid}] ) changelog

	                JOIN {logTableFullName} changelogdata ON changelogdata.[{id}] = changelog.Token
	                LEFT JOIN {tableFullName} ON {tableFullName}.[{id}] = changelog.[{fid}]
         
                WHERE Action <> 'Delete'
                ORDER BY Token
            ");

            this["InsertChange"] = Vars.Format(
                "INSERT INTO {logTableFullName} ( [{fid}], [{action}], [{data}] ) "
                + "OUTPUT INSERTED.* "
                + "VALUES( @{fid}, @{action}, @{data} );");

            this["ChangeLogIdFidIndex"] = Vars.Format(@"
                CREATE NONCLUSTERED INDEX [{logTableName}.id_fid_index] ON {logTableFullName}
                (
	                [Id] ASC,
	                [Fid] ASC
                ) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
            ");

            this["ChangeLogIdFidActionIndex"] = Vars.Format(@"
                CREATE NONCLUSTERED INDEX [{logTableName}.id_fid_action_index] ON {logTableFullName}
                (
	                [Id] ASC,
	                [Fid] ASC,
	                [Action] ASC
                ) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
            ");

            this["ChangeLogFidIdIndex"] = Vars.Format(@"
                CREATE NONCLUSTERED INDEX [{logTableName}.fid_id_index] ON {logTableFullName}
                (
	                [Fid] ASC,
	                [Id] ASC
                ) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
            ");

            this["ChangeLogIndexes"] = Vars.Format(@"
                SELECT * 
                FROM sys.indexes 
                WHERE name LIKE '{logTableName}.%' AND object_id = OBJECT_ID('{logTableFullName}');
            ");

            
            this["SelectChangesObserver"] = Vars.Format(@"
                SELECT
	                {tableFullName}.Id,
	                {tableFullName}.Reference, 
	                {tableFullName}.Version, 
	                {tableFullName}.ContentType, 
	                {tableFullName}.Created, 
	                {tableFullName}.Updated, 
	                {tableFullName}.Data, 

	                changelogdata.[Action] AS [Action],
	                changelog.[Token],
	                changelog.[{fid}],
                    changelogdata.[Data] as Diff

                FROM ( 
	                SELECT MAX([{id}]) as Token, [{fid}] 
	                FROM {logTableFullName}
	                WHERE [{id}] > @token
	                GROUP BY [{fid}]) changelog

	                JOIN {logTableFullName} changelogdata ON changelogdata.[{id}] = changelog.Token
	                LEFT JOIN {tableFullName} ON {tableFullName}.[{id}] = changelog.[{fid}]
                ORDER BY Token
            ");

            this["SelectChangesObserverInit"] = Vars.Format(@"
                SELECT
	                {tableFullName}.Id,
	                {tableFullName}.Reference, 
	                {tableFullName}.Version, 
	                {tableFullName}.ContentType, 
	                {tableFullName}.Created, 
	                {tableFullName}.Updated, 
	                {tableFullName}.Data, 

	                changelogdata.[Action] AS [Action],
	                changelog.[Token],
	                changelog.[{fid}],
                    changelogdata.[Data] as Diff

                FROM ( 
	                SELECT MAX([{id}]) as Token, [{fid}] 
	                FROM {logTableFullName}
	                WHERE [{id}] > @token AND [{id}] <= (SELECT MAX({id}) FROM {logTableFullName})
	                GROUP BY [{fid}] ) changelog

	                JOIN {logTableFullName} changelogdata ON changelogdata.[{id}] = changelog.Token
	                LEFT JOIN {tableFullName} ON {tableFullName}.[{id}] = changelog.[{fid}]
         
                WHERE Action <> 'Delete'
                ORDER BY Token
            ");







        }
    }
}
