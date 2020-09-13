using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotJEM.Json.Storage.Adapter;
using DotJEM.Json.Storage.Adapter.Materialize.ChanceLog;
using DotJEM.Json.Storage.Adapter.Materialize.ChanceLog.ChangeObjects;
using DotJEM.Json.Storage.Adapter.Materialize.Log;
using DotJEM.Json.Storage.Stress.Index;
using DotJEM.Json.Storage.Stress.Logging;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Stress
{
    class Program
    {
        public static string ConnectionString => Environment.GetEnvironmentVariable("appveyor_sqlconnection") ?? "Data Source=.\\DEV;Initial Catalog=json;Integrated Security=True";

        static void Main(string[] args)
        {
            ChangesIndex index = new ChangesIndex();
            ChangeProvider provider = new ChangeProvider(100, 30, 0);

            IStorageContext context = new SqlServerStorageContext(ConnectionString);
            context.Configure.ReadCommandTimeout = 120;

            Scheduler scheduler = new Scheduler();

            IEnumerable<ChangeProducer> producers = from i in Enumerable.Range(0, 25)
                                                    select new ChangeProducer(context.Area("stress"), provider, index, new QueueingLogWriter($"producer-{i:000}.out", 1024 * 1024 * 10, 10, false));

            ChangeConsumer consumer = new ChangeConsumer(context, "stress");

            scheduler.Start("Consume Updates.", consumer.Execute, 10.Seconds());

            producers = producers.Select((producer, i) =>
            {
                scheduler.Start($"Producer {i:000}.", producer.Execute, 200.Milliseconds());
                return producer;
            }).ToArray();

            Console.ReadLine();
        }
    }

    public class ChangeProvider
    {
        private readonly Random random = new Random();
        private readonly ChangeType[] distribution;

        public ChangeProvider(int create, int update, int delete)
        {
            distribution = Enumerable.Repeat(ChangeType.Create, create)
                .Concat(Enumerable.Repeat(ChangeType.Update, update))
                .Concat(Enumerable.Repeat(ChangeType.Delete, delete))
                .Select(type => new {type, i = random.Next()})
                .OrderBy(x => x.i)
                .Select(x => x.type)
                .ToArray();
        }

        public Change Next(long index)
        {
            if (index == 0)
                return new Change(ChangeType.Create, -1);

            ChangeType type = distribution[random.Next(distribution.Length - 1)];
            if (type == ChangeType.Delete || type == ChangeType.Update)
                return new Change(type, LongRandom(index - 1));
            return new Change(type, -1);
        }

        private long LongRandom(long max)
        {
            if (max <= int.MaxValue)
                return random.Next((int) max);

            byte[] buf = new byte[8];
            random.NextBytes(buf);
            long longRand = BitConverter.ToInt64(buf, 0);
            return Math.Abs(longRand)%max;
        }
    }

    public class Change
    {
        public long Index { get; }
        public ChangeType Type { get; }

        public Change(ChangeType type, long index)
        {
            Type = type;
            Index = index;
        }
    }

    public class ChangeProducer
    {
        private readonly IStorageArea area;
        private readonly ChangeProvider provider;
        private readonly ChangesIndex index;
        private readonly ILogWriter logger;

        //private BigList<Guid> entities = new BigList<Guid>();

        public ChangeProducer(IStorageArea area, ChangeProvider provider, ChangesIndex index, ILogWriter logger)
        {
            this.area = area;
            this.provider = provider;
            this.index = index;
            this.logger = logger;
        }

        public void Execute()
        {
            Change change = provider.Next(0);
            Guid id = change.Type != ChangeType.Create ? index[change.Index] : Guid.Empty;

            Stopwatch t = Stopwatch.StartNew();
            switch (change.Type)
            {
                case ChangeType.Create:
                    JObject entity = area.Insert("dummy", new JObject());
                    t.Stop();
                    logger.Write($"Execution of 'INSERT' took {t.ElapsedMilliseconds} ms");
                    index.Add((Guid) entity["$id"]);
                    break;
                case ChangeType.Update:
                    if (index.Count < 1)
                        break;

                    area.Update(id, new JObject());
                    t.Stop();
                    logger.Write($"Execution of 'UPDATE' took {t.ElapsedMilliseconds} ms");
                    break;
                case ChangeType.Delete:
                    if (index.Count < 1)
                        break;

                    area.Delete(id);
                    index.Delete(id);

                    t.Stop();
                    logger.Write($"Execution of 'DELETE' took {t.ElapsedMilliseconds} ms");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public class ChangeConsumer
    {
        private readonly Dictionary<string, IStorageAreaLog> logs = new Dictionary<string, IStorageAreaLog>();

        public ChangeConsumer(IStorageContext storage, params string[] areas)
        {
            this.logs = areas.Select(area => new {area, log = storage.Area(area).Log}).ToDictionary(kv => kv.area, kv => kv.log);
        }

        public void Execute()
        {
            try
            {
                while (true)
                {
                    Stopwatch timer = Stopwatch.StartNew();
                    IEnumerable<Tuple<string, IStorageChangeCollection>> tuples = logs.Select(log => new Tuple<string, IStorageChangeCollection>(log.Key, log.Value.Get())).ToList();

                    var sum = tuples.Sum(t => t.Item2.Count.Total);
                    if (sum < 1)
                        return;

                    // ReSharper disable ReturnValueOfPureMethodIsNotUsed
                    //  - TODO: Using SYNC here is a hack, ideally we would wan't to use a prober Async pattern, 
                    //          but this requires a bigger refactoring.
                    tuples.Select(WriteChanges).ToArray();
                    // ReSharper restore ReturnValueOfPureMethodIsNotUsed
                    timer.Stop();
                    Console.WriteLine($"{DateTime.Now:s} Execution of 'ChangeConsumer' took {timer.ElapsedMilliseconds} ms");

                    if (sum < 5000)
                        return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private long WriteChanges(Tuple<string, IStorageChangeCollection> tuple)
        {
            IStorageChangeCollection changes = tuple.Item2;
            Console.WriteLine($" -> {changes.Count}");
            return changes.Token;
        }
    }

    public static class IntTimeSpanExt
    {
        public static TimeSpan Seconds(this int self)
        {
            return TimeSpan.FromSeconds(self);
        }

        public static TimeSpan Milliseconds(this int self)
        {
            return TimeSpan.FromMilliseconds(self);
        }
    }

    public class Scheduler
    {
        private readonly ILogWriter log = new QueueingLogWriter("schedulee.log", 1024*1024*32, 5, false);
        private readonly List<ScheduledTask> tasks = new List<ScheduledTask>();

        public void Start(string name, Action update, TimeSpan period) => tasks.Add(new ScheduledTask(name, update, period, log));

        public void Stop()
        {
            foreach (ScheduledTask task in tasks) task.Dispose();
        }
    }

    public class ScheduledTask : IDisposable
    {
        private readonly Action update;
        private readonly TimeSpan period;
        private readonly ILogWriter log;
        private readonly AutoResetEvent handle = new AutoResetEvent(false);

        private bool disposed;
        private RegisteredWaitHandle executing;

        public string Name { get; }

        public ScheduledTask(string name, Action update, TimeSpan period, ILogWriter log)
        {
            this.update = update;
            this.period = period;
            this.log = log;
            this.Name = name;

            Next();
        }

        private void Next()
        {
            if (disposed)
                return;

            executing = ThreadPool.RegisterWaitForSingleObject(handle, (state, timedout) => ExecuteCallback(), null, period, true);
        }

        private void ExecuteCallback()
        {
            if (disposed)
                return;

            try
            {
                update();
            }
            catch (Exception ex)
            {
                log.Write($"Execution of '{Name}' failed.");
                log.Write(ex);
                // ignored
            }
            Next();
        }

        public void Dispose()
        {
            disposed = true;
            executing.Unregister(null);
        }
    }
}
