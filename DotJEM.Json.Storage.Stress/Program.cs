using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotJEM.Json.Storage.Adapter;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Stress
{
    class Program
    {
        public static string ConnectionString => Environment.GetEnvironmentVariable("appveyor_sqlconnection") ?? "Data Source=.\\DEV;Initial Catalog=json;Integrated Security=True";

        static void Main(string[] args)
        {
            ChangeProvider provider = new ChangeProvider(100, 30, 0);

            IStorageContext context = new SqlServerStorageContext(ConnectionString);

            Scheduler scheduler = new Scheduler();
            ChangeProducer producer1 = new ChangeProducer(context.Area("stress"), provider);
            ChangeProducer producer2 = new ChangeProducer(context.Area("stress"), provider);
            ChangeProducer producer3 = new ChangeProducer(context.Area("stress"), provider);
            ChangeProducer producer4 = new ChangeProducer(context.Area("stress"), provider);
            ChangeProducer producer5 = new ChangeProducer(context.Area("stress"), provider);

            ChangeConsumer consumer = new ChangeConsumer(context, "stress");

            scheduler.Start("Consume Updates.", consumer.Execute, 10.Seconds());

            scheduler.Start("Producer 1.", producer1.Execute, 100.Milliseconds());
            scheduler.Start("Producer 2.", producer2.Execute, 100.Milliseconds());
            scheduler.Start("Producer 3.", producer3.Execute, 100.Milliseconds());
            scheduler.Start("Producer 4.", producer4.Execute, 100.Milliseconds());
            scheduler.Start("Producer 5.", producer5.Execute, 100.Milliseconds());

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
                .Select(type => new { type, i = random.Next() })
                .OrderBy(x => x.i)
                .Select(x => x.type)
                .ToArray();
        }

        public Change Next(long index)
        {
            if(index == 0)
                return new Change(ChangeType.Create, -1);

            ChangeType type = distribution[random.Next(distribution.Length-1)];
            if(type == ChangeType.Delete || type == ChangeType.Update)
                return new Change(type, LongRandom(index-1));
            return new Change(type, -1);
        }

        private long LongRandom(long max)
        {
            if (max <= int.MaxValue)
                return random.Next((int) max);

            byte[] buf = new byte[8];
            random.NextBytes(buf);
            long longRand = BitConverter.ToInt64(buf, 0);
            return Math.Abs(longRand) % max;
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

        //private BigList<Guid> entities = new BigList<Guid>();

        public ChangeProducer(IStorageArea area, ChangeProvider provider)
        {
            this.area = area;
            this.provider = provider;
        }

        public void Execute()
        {
            Change change = provider.Next(0);
            //Guid id = change.Type !=  ChangeType.Create ? entities[change.Index] : Guid.Empty;

            switch (change.Type)
            {
                case ChangeType.Create:
                    Stopwatch t = Stopwatch.StartNew();
                    JObject entity = area.Insert("dummy", new JObject());
                    t.Stop();
                    Console.WriteLine($"Execution of 'INSERT' took {t.ElapsedMilliseconds} ms");
                    //entities.Add((Guid)entity["$id"]);
                    break;
                case ChangeType.Update:
                    //if(entities.Count < 1)
                    //    break;

                    //area.Update(id, new JObject());
                    break;
                case ChangeType.Delete:
                    //if (entities.Count < 1)
                    //    break;

                    //area.Delete(id);
                    //entities.Delete(id);

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
            IEnumerable<Tuple<string, IStorageChanges>> tuples = logs.Select(log => new Tuple<string, IStorageChanges>(log.Key, log.Value.Get())).ToList();

            if (tuples.Sum(t => t.Item2.Count.Total) < 1)
                return;

            // ReSharper disable ReturnValueOfPureMethodIsNotUsed
            //  - TODO: Using SYNC here is a hack, ideally we would wan't to use a prober Async pattern, 
            //          but this requires a bigger refactoring.
            tuples.Select(WriteChanges).ToArray();
            // ReSharper restore ReturnValueOfPureMethodIsNotUsed
        }

        private long WriteChanges(Tuple<string, IStorageChanges> tuple)
        {
            IStorageChanges changes = tuple.Item2;
            Console.WriteLine($"Created: {changes.Count.Created}");
            Console.WriteLine($"Updated: {changes.Count.Updated}");
            Console.WriteLine($"Deleted: {changes.Count.Deleted}");
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
        private readonly List<ScheduledTask> tasks = new List<ScheduledTask>();

        public void Start(string name, Action update, TimeSpan period) => tasks.Add(new ScheduledTask(name, update, period));

        public void Stop()
        {
            foreach (ScheduledTask task in tasks) task.Dispose();
        }
    }

    public class ScheduledTask : IDisposable
    {
        private readonly Action update;
        private readonly TimeSpan period;
        private readonly AutoResetEvent handle = new AutoResetEvent(false);

        private bool disposed;
        private RegisteredWaitHandle executing;

        public string Name { get; }

        public ScheduledTask(string name, Action update, TimeSpan period)
        {
            this.update = update;
            this.period = period;
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
                Stopwatch t = Stopwatch.StartNew();
                update();
                t.Stop();
                Console.WriteLine($"Execution of '{Name}' took {t.ElapsedMilliseconds} ms");


                Next();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Execution of '{Name}' failed.");
                Console.WriteLine(ex);

                // ignored
            }
        }

        public void Dispose()
        {
            disposed = true;
            executing.Unregister(null);
        }
    }

    //public class BigList<T>
    //{
    //    private const int PART_SIZE = 10000000;

    //    private readonly List<List<T>> storage = new List<List<T>>();

    //    public long Count { get; private set; }

    //    public void Add(T item)
    //    {
    //        List<T> partition = GetPartition(Count);
    //        partition.Add(item);
    //        Count++;
    //    }

    //    public T this[long i]
    //    {
    //        get
    //        {
    //            List<T> partition = GetPartition(i);
    //            int index = (int) (i%PART_SIZE);
    //            return partition[index];
    //        }
    //    }

    //    private List<T> GetPartition(long index)
    //    {
    //        int partitionIndex = (int) (index/PART_SIZE);
    //        if (storage.Count < partitionIndex)
    //            storage.Add(new List<T>());
    //        return storage[partitionIndex];
    //    }

    //    public void Delete(Guid id)
    //    {
            
    //    }
    //}
}
