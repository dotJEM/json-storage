using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotJEM.Json.Storage.Adapter;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DotJEM.Json.Storage.Benchmarks
{
    [TestFixture]
    public class InsertOnSimpleStorageBenchmarks
    {
        [Test]
        public void Benchmark()
        {
            IStorageContext context = new SqlServerStorageContext("Data Source=.\\DEV;Initial Catalog=json;Integrated Security=True");
            IStorageArea area = context.Area("simple");

            TestObjectGenerator generator = new TestObjectGenerator();

            //AutoResetEvent exit = new AutoResetEvent(false);
            //ThreadPool.RegisterWaitForSingleObject(exit, (o, b) => generator.Stop(), null, TimeSpan.FromMinutes(5), false);
            Stopwatch timer = Stopwatch.StartNew();

            List<IGrouping<long, long>> results = 
            generator.AsParallel().Select(doc =>
            {
                area.Insert(doc.ContentType, doc.Data);
                if (timer.ElapsedMilliseconds/(1000*30) > 0)
                {
                    generator.Stop();
                }
                return timer.ElapsedMilliseconds;

            }).GroupBy(ms => ms / 1000).OrderBy(g => g.Key).ToList();

            foreach (IGrouping<long, long> longs in results)
            {
                Console.WriteLine(longs.Key + " == " + longs.Count());
            }

        }


    }



    public class BenchmarkingEngine
    {
        private readonly TestObjectGenerator generator = new TestObjectGenerator();
        private readonly IStorageContext context = new SqlServerStorageContext("Data Source=.\\DEV;Initial Catalog=json;Integrated Security=True");
        private readonly IStorageArea area;


        public BenchmarkingEngine()
        {
            area = context.Area();
        }

        public void Start()
        {
            Stopwatch timer = Stopwatch.StartNew();
            generator.AsParallel().ForAll(doc =>
            {
                area.Insert(doc.ContentType, doc.Data);
                long sec = timer.ElapsedMilliseconds / 1000;
            });

            //generator.AsParallel().Select(doc =>
            //{
            //    area.Insert(doc.ContentType, doc.Data);
            //    return timer.ElapsedMilliseconds/1000;
            //});
        }

        public void Stop()
        {
            generator.Stop();
        }
    }

    public class Document
    {
        public Guid Id { get; set; }
        public JObject Data { get; private set; }
        public string ContentType { get; private set; }

        public Document(string contentType, JObject data)
        {
            Data = data;
            ContentType = contentType;
        }
    }

    public class TestObjectGenerator : IEnumerable<Document>
    {
        private bool stop = false;
        private readonly Random rand = new Random(42);
        private readonly HashSet<string> contentTypes = new HashSet<string>(new[] { "order", "person", "product", "account", "storage", "address", "payment", "delivery", "token", "shipment" });

        public IEnumerator<Document> GetEnumerator()
        {
            while (!stop)
            {
                yield return new Document(
                    RandomContentType(),
                    RandomDocument()
                    );
            }
        }

        private string RandomContentType()
        {
            return contentTypes.ElementAt(rand.Next(0, contentTypes.Count()));
        }

        private JObject RandomDocument()
        {
            return JObject.FromObject(new { A = "Item 42" });
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Stop()
        {
            stop = true;
        }
    }

}
