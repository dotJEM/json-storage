using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DotJEM.Json.Storage.Adapter;
using DotJEM.Json.Storage.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Debug
{
    class Program
    {
        static void Main(string[] args)
        {
            Command command = Command.Parse(args);

            IStorageContext context = new SqlServerStorageContext(command.ConnectionString);
            context.Configure.MapField(JsonField.Id, "id");
            IStorageArea area = context.Area(command.Area);

            try
            {
                Execute(command, area);

            }
            catch (InvalidCommandException ex)
            {
                Console.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private static void Execute(Command command, IStorageArea area)
        {
            switch (command.Type)
            {
                case CommandType.Get:

                    if (command.Id != Guid.Empty)
                    {
                        dynamic json = area.Get(command.Id);
                        string file = $"{json.id}.json";
                        Console.WriteLine("READING TO FILE: {0}", file);
                        File.WriteAllText(file, ((JObject)json).ToString(Formatting.Indented));
                    }
                    else
                    {
                        foreach (dynamic json in area.Get(command.ContentType))
                        {
                            string file = $"{json.id}.json";
                            Console.WriteLine("READING TO FILE: {0}", file);
                            File.WriteAllText(file, ((JObject)json).ToString(Formatting.Indented));
                        }
                    }

                    break;
                case CommandType.List:
                    Console.WriteLine($"Documents of type '{command.ContentType}': ");
                    foreach (dynamic json in area.Get(command.ContentType))
                        Console.WriteLine($" > {json.id}");
                    break;
                case CommandType.Insert:
                    Console.WriteLine("CREATED: {0}", area.Insert(command.ContentType, command.Document)["id"]);
                    break;
                case CommandType.Update:
                    Console.WriteLine("UPDATED: {0}", area.Update(command.Id, command.Document)["id"]);
                    break;
                case CommandType.Delete:
                    Console.WriteLine("DELETED: {0}", area.Delete(command.Id)["id"]);
                    break;
                default:
                    Console.WriteLine("INVALID COMMAND");
                    break;
            }
        }
    }

    public class Command
    {
        private static readonly Regex insertCommand = new Regex(@"^Insert\s(?'type'\w+)\((?'json'\{.*\})\)\sinto\s(?'area'\w+)\s-conn=(?'conn'.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex listCommand = new Regex(@"^List\s(?'type'\w+)\sfrom\s(?'area'\w+)\s-conn=(?'conn'.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex getSingleCommand = new Regex(@"^Get\s(?'id'[{|\(]?[0-9A-F]{8}[-]?([0-9A-F]{4}[-]?){3}[0-9A-F]{12}[\)|}]?)\sfrom\s(?'area'\w+)\s-conn=(?'conn'.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex getMultipleCommand = new Regex(@"^Get\s(?'type'\w+)\sfrom\s(?'area'\w+)\s-conn=(?'conn'.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        //^Get\s([{|\(]?[0-9A-F]{8}[-]?([0-9A-F]{4}[-]?){3}[0-9A-F]{12}[\)|}]?)\sfrom\s(\w+)\s-conn=(.*)

        public static Command Parse(string[] args)
        {
            string full = string.Join(" ", args);
            switch (args.First().ToLower())
            {
                case "insert":
                    return CreateInsertCommand(full);

                case "list":
                    return CreateListCommand(full);

                case "get":
                    var matchSingle = getSingleCommand.Match(full);
                    Command command = new Command();
                    if (!matchSingle.Success)
                    {
                        var matchMultiple = getMultipleCommand.Match(full);
                        if (!matchMultiple.Success)
                        {
                            throw new InvalidCommandException(full);
                        }

                        command.Type = CommandType.Get;
                        command.Area = matchMultiple.Groups["area"].Value;
                        command.ContentType = matchMultiple.Groups["type"].Value;
                        command.ConnectionString = matchMultiple.Groups["conn"].Value;
                        return command;
                    }

                    command.Type = CommandType.List;
                    command.Area = matchSingle.Groups["area"].Value;
                    command.Id = Guid.Parse(matchSingle.Groups["id"].Value);
                    command.ConnectionString = matchSingle.Groups["conn"].Value;
                    return command;

            }

            return new Command();
        }

        private static Command CreateListCommand(string full)
        {
            var match = listCommand.Match(full);
            if (!match.Success)
            {
                throw new InvalidCommandException(full);
            }

            Command command = new Command();
            command.Type = CommandType.List;
            command.Area = match.Groups["area"].Value;
            command.ContentType = match.Groups["type"].Value;
            command.ConnectionString = match.Groups["conn"].Value;
            return command;
        }

        private static Command CreateInsertCommand(string full)
        {
            Command command = new Command();
            var match = insertCommand.Match(full);
            if (!match.Success)
            {
                throw new InvalidCommandException(full);
            }

            command.Type = CommandType.Insert;
            command.Document = JObject.Parse(match.Groups["json"].Value);
            command.Area = match.Groups["area"].Value;
            command.ContentType = match.Groups["type"].Value;
            command.ConnectionString = match.Groups["conn"].Value;
            return command;
        }

        public string ConnectionString { get; set; }
        public string Area { get; set; }
        public CommandType Type { get; set; }
        public string ContentType { get; set; }
        public JObject Document { get; set; }
        public Guid Id { get; set; }
    }

    public class InvalidCommandException : Exception
    {
        public InvalidCommandException(string full) : base($"'{full}' Was not a valid command.")
        {

        }
    }

    public enum CommandType
    {
        Get,
        List,
        Insert,
        Update,
        Delete
    }
}
