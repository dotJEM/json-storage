using System.Collections.Generic;
using System.Dynamic;
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

    public class SqlServerCommandFactory : DynamicObject, ICommandFactory
    {
        private readonly dynamic self;
        private readonly Dictionary<string, object> commands = new Dictionary<string, object>();
        private readonly AdvPropertyBag vars = new AdvPropertyBag("{", "}");

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
            //TODO: Now er can use C#'s new interpolation feature.
            vars.Add("id", StorageField.Id)
                .Add("ref", StorageField.Reference)
                .Add("version", StorageField.Version)
                .Add("type", StorageField.ContentType)
                .Add("created", StorageField.Created)
                .Add("updated", StorageField.Updated)
                .Add("data", StorageField.Data);

            vars.Add("fid", HistoryField.Fid)
                .Add("deleted", HistoryField.Deleted);

            vars.Add("action", LogField.Action);

            vars.Add("tableName", table)
                .Add("historyTableName", "{tableName}.history")
                .Add("seedTableName", "{tableName}.seed")
                .Add("logTableName", "{tableName}.changelog")
                .Add("db", database)
                .Add("tableFullName", "[{db}].[dbo].[{tableName}]")
                .Add("historyTableFullName", "[{db}].[dbo].[{historyTableName}]")
                .Add("seedTableFullName", "[{db}].[dbo].[{seedTableName}]")
                .Add("logTableFullName", "[{db}].[dbo].[{logTableName}]");

            //TODO: Replace with a command builder pattern.
            self.Insert = vars.Format(
                "INSERT INTO {tableFullName} ( [{version}], [{type}], [{created}], [{updated}], [{data}], [{ref}] ) "
                + "OUTPUT INSERTED.* "
                + "SELECT [next_{version}], [next_{type}], [next_{created}], [next_{updated}], [next_{data}], [next_{ref}] "
                + "    FROM ( MERGE {seedTableFullName} WITH (HOLDLOCK) AS seedtable "
                + "           USING (SELECT @{type} as [{type}]) AS next_ref ON seedtable.[{type}] = next_ref.[{type}] "
                + "           WHEN MATCHED THEN        UPDATE SET seedtable.Seed = seedtable.Seed + 1 "
                + "           WHEN NOT MATCHED THEN    INSERT ([{type}]) VALUES (@{type}) "
                + "           OUTPUT 1, @{type}, @{created}, @{updated}, @{data}, INSERTED.Seed as [{ref}] ) "
                + "        as TEMP( [next_{version}], [next_{type}], [next_{created}], [next_{updated}], [next_{data}], [next_{ref}] );");

            self.Update = vars.Format(
                "UPDATE {tableFullName} SET [{version}] = [{version}] + 1, [{updated}] = @{updated}, [{data}] = @{data}"
                          + " OUTPUT"
                          + "   DELETED.[{id}] as [DELETED_{id}], DELETED.[{ref}] as [DELETED_{ref}], DELETED.[{version}] as [DELETED_{version}], "
                          + "   DELETED.[{type}] as [DELETED_{type}], DELETED.[{created}] as [DELETED_{created}], "
                          + "   DELETED.[{updated}] as [DELETED_{updated}], DELETED.[{data}] as [DELETED_{data}],"
                          + "   INSERTED.[{id}] as [INSERTED_{id}], INSERTED.[{ref}] as [INSERTED_{ref}], INSERTED.[{version}] as [INSERTED_{version}], "
                          + "   INSERTED.[{type}] as [INSERTED_{type}], INSERTED.[{created}] as [INSERTED_{created}], "
                          + "   INSERTED.[{updated}] as [INSERTED_{updated}], INSERTED.[{data}] as [INSERTED_{data}]"
                          + " WHERE [{id}] = @{id};");
            self.Delete = vars.Format("DELETE FROM {tableFullName} OUTPUT DELETED.* WHERE [{id}] = @{id};");

            //TODO: Requires paging!
            self.Count = vars.Format("SELECT COUNT_BIG([{id}]) FROM {tableFullName};");
            self.CountByContentType = vars.Format("SELECT COUNT_BIG([{id}]) FROM {tableFullName} WHERE [{type}] = @{type};");

            self.SelectAll = vars.Format("SELECT * FROM {tableFullName} ORDER BY [{created}];");
            self.SelectAllByContentType = vars.Format("SELECT * FROM {tableFullName} WHERE [{type}] = @{type} ORDER BY [{created}];");
            self.SelectSingle = vars.Format("SELECT * FROM {tableFullName} WHERE [{id}] = @{id} ORDER BY [{created}];");

            self.SelectAllPaged = vars.Format(@"
                SELECT  *
                FROM    ( SELECT    ROW_NUMBER() OVER ( ORDER BY [{created}] ) AS rn, *
                          FROM      {tableFullName}
                        ) AS PagedResult
                WHERE   rn >= @rowstart
                    AND rn < @rowend
            ");


            self.SelectAllPagedByContentType = vars.Format(@"
                SELECT  *
                FROM    ( SELECT    ROW_NUMBER() OVER ( ORDER BY [{created}] ) AS rn, *
                          FROM      {tableFullName}
                          WHERE [{type}] = @{type}
                        ) AS PagedResult
                WHERE   rn >= @rowstart
                    AND rn < @rowend
            ");

            /*
            Table Spec:
                Name: 
                  {tableName}

                Columns:
                  {id}] uniqueidentifier NOT NULL
                  {ref}] bigint NOT NULL
                  {version} int NOT NULL
                  {type} varchar(256) NOT NULL
                  {created} datetime NOT NULL
                  {updated} datetime NOT NULL
                  {data} varbinary(max) NOT NULL
                  RV rowversion NOT NULL
                  
                Constraints:
                  PK_{tableName} PRIMARY KEY CLUSTERED ( Id ASC )
                    With:
                      PAD_INDEX OFF
                      STATISTICS_NORECOMPUTE = OFF
                      IGNORE_DUP_KEY = OFF
                      ALLOW_ROW_LOCKS = ON
                      ALLOW_PAGE_LOCKS = ON
            
            */

            //dynamic spec = null;// = new spec("");

            //spec
            //    .Column("id", "uniqueidentifier", "not null")
            //    .Column("ref", "uniqueidentifier", "not null")
            //    .Column("version", "uniqueidentifier", "not null")
            //    .Column("type", "uniqueidentifier", "not null")
            //    .Column("updated", "uniqueidentifier", "not null")
            //    .Column("data", "uniqueidentifier", "not null")
            //    .Column("RV", "uniqueidentifier", "not null")
            //    .Constraint()
            //    .Index();


            self.CreateTable = vars.Format(
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

            self.TableExists = vars.Format(
                @"SELECT TOP 1 COUNT(*) FROM INFORMATION_SCHEMA.TABLES
                               WHERE TABLE_SCHEMA = 'dbo'
                                 AND TABLE_NAME = '{tableName}'");


            // HISTORY STUFF
            self.InsertHistory = vars.Format("INSERT INTO {historyTableFullName} ([{fid}], [{ref}], [{version}], [{type}], [{deleted}], [{created}], [{updated}], [{data}])"
                                           + " VALUES (@{fid}, @{ref}, @{version}, @{type}, @{deleted}, @{created}, @{updated}, @{data});");
            //TODO: Requires paging!
            self.SelectHistoryFor = vars.Format("SELECT * FROM {historyTableFullName} WHERE [{fid}] = @{fid} ORDER BY [{version}] DESC;");
            self.SelectDeletedHistoryByContentType = vars.Format("SELECT * FROM {historyTableFullName} WHERE [{deleted}] = 1 AND [{type}] = @{type} ORDER BY [{version}];");

            self.SelectHistoryForByVersion = vars.Format("SELECT * FROM {historyTableFullName} WHERE [{fid}] = @{fid} AND [{version}] = @{version}");
            self.SelectHistoryForFromDate = vars.Format("SELECT * FROM {historyTableFullName} WHERE [{fid}] = @{fid} AND [{updated}] >= @{updated} ORDER BY [{version}] DESC;");
            self.SelectHistoryForToDate = vars.Format("SELECT * FROM {historyTableFullName} WHERE [{fid}] = @{fid} AND [{updated}] <= @{updated} ORDER BY [{version}] DESC;");
            self.SelectHistoryForBetweenDate = vars.Format("SELECT * FROM {historyTableFullName} WHERE [{fid}] = @{fid} AND [fromdate] >= @fromdate AND [todate] <= @todate ORDER BY [{version}] DESC;");
            self.SelectDeletedHistoryByContentTypeFromDate = vars.Format("SELECT * FROM {historyTableFullName} WHERE [{deleted}] = 1 AND [{updated}] >= @{updated} AND [{type}] = @{type} ORDER BY [{version}];");

            self.CreateHistoryTable = vars.Format(
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

            self.HistoryTableExists = vars.Format(
                @"SELECT TOP 1 COUNT(*) FROM INFORMATION_SCHEMA.TABLES
                                            WHERE TABLE_SCHEMA = 'dbo'
                                                AND TABLE_NAME = '{historyTableName}'");

            // SEED STUFF
            self.CreateSeedTable = vars.Format(
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

            self.SeedTableExists = vars.Format(
                @"SELECT TOP 1 COUNT(*) FROM INFORMATION_SCHEMA.TABLES
                               WHERE TABLE_SCHEMA = 'dbo'
                                 AND TABLE_NAME = '{seedTableName}'");

            // logTableName
            // logTableFullName

            // CHANGE LOG
            self.CreateLogTable = vars.Format(
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


            self.LogTableExists = vars.Format(
                @"SELECT TOP 1 COUNT(*) FROM INFORMATION_SCHEMA.TABLES
                               WHERE TABLE_SCHEMA = 'dbo'
                                 AND TABLE_NAME = '{logTableName}'");

            self.SelectChanges = vars.Format("SELECT * FROM {logTableFullName} WHERE [{id}] > @token;");

            self.SelectChangesWithDeletes = vars.Format(@"
                SELECT TOP 5000
	                {tableFullName}.Id,
	                {tableFullName}.Reference, 
	                {tableFullName}.Version, 
	                {tableFullName}.ContentType, 
	                {tableFullName}.Created, 
	                {tableFullName}.Updated, 
	                {tableFullName}.Data, 


	                changelogdata.[Action] AS [Action],
	                changelogdata.[Data] AS [Payload],
	                changelog.[Token],
	                changelog.[{fid}]

                FROM ( 
	                SELECT MAX([{id}]) as Token, [{fid}] 
	                FROM {logTableFullName}
	                WHERE [{id}] > @token
	                GROUP BY [{fid}] ) changelog

	                JOIN {logTableFullName} changelogdata ON changelogdata.[{id}] = changelog.Token
	                LEFT JOIN {tableFullName} ON {tableFullName}.[{id}] = changelog.[{fid}]
            ");

            self.SelectChangesNoDeletes = vars.Format(@"
                SELECT TOP 5000
	                {tableFullName}.Id,
	                {tableFullName}.Reference, 
	                {tableFullName}.Version, 
	                {tableFullName}.ContentType, 
	                {tableFullName}.Created, 
	                {tableFullName}.Updated, 
	                {tableFullName}.Data, 


	                changelogdata.[Action] AS [Action],
	                changelogdata.[Data] AS [Payload],
	                changelog.[Token],
	                changelog.[{fid}]

                FROM ( 
	                SELECT MAX([{id}]) as Token, [{fid}] 
	                FROM {logTableFullName}
	                WHERE [{id}] > @token
	                GROUP BY [{fid}] ) changelog

	                JOIN {logTableFullName} changelogdata ON changelogdata.[{id}] = changelog.Token
	                LEFT JOIN {tableFullName} ON {tableFullName}.[{id}] = changelog.[{fid}]
         
                WHERE Action <> 'Delete'
            ");

            self.InsertChange = vars.Format(
                "INSERT INTO {logTableFullName} ( [{fid}], [{action}], [{data}] ) "
                + "OUTPUT INSERTED.* "
                + "VALUES( @{fid}, @{action}, @{data} );");
        }
    }
}
