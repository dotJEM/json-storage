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

            IEnumerable<int> counts =
            generator.AsParallel().Select(doc =>
            {
                area.Insert(doc.ContentType, doc.Data);
                if (timer.ElapsedMilliseconds / (1000 * 30) > 0)
                {
                    generator.Stop();
                }
                return timer.ElapsedMilliseconds;
            }).GroupBy(ms => ms / 1000).OrderBy(g => g.Key).Select(group => group.Count()).ToList();

            //NOTE: 5000 Inserts pr. second, aint that ok?
            Assert.That(counts.Average(), Is.GreaterThan(5000));
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
            generator.AsParallel().ForAll(doc => area.Insert(doc.ContentType, doc.Data));
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
        private readonly HashSet<string> contentTypes = new HashSet<string>(new[] { "order", "person", "product", "account", "storage", "address", "payment", "delivery", "token", "shipment" });

        public IEnumerator<Document> GetEnumerator()
        {
            while (!stop)
            {
                string contentType = RandomContentType();
                yield return new Document(contentType, RandomDocument(contentType));
            }
        }

        private string RandomContentType()
        {
            return contentTypes.RandomItem();
        }

        private JObject RandomDocument(string contentType)
        {

            //TODO: Bigger document and use contentype for propper stuff.
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

    public static class RandomHelper
    {
        private static readonly Random rand = new Random(42);

        public static T RandomItem<T>(this IEnumerable<T> items)
        {
            ICollection<T> list = items as ICollection<T> ?? items.ToArray();
            return list.ElementAt(rand.Next(0, list.Count()));
        }
    }

    public class RandomTextGenerator
    {
        private readonly string[] texts = "Childharold,Decameron,Faust,Inderfremde,Lebateauivre,Lemasque,Loremipsum,Nagyonfaj,Omagyar,Robinsonokruso,Theraven,Tierrayluna".Split(',');

        public string Paragraph(string @from, int count)
        {
            string words = LoremIpsums.ResourceManager.GetString(@from, LoremIpsums.Culture);
        }

        public string Word(string @from, int minlength)
        {
            return Open(from).Where(w => w.Length >= minlength).RandomItem();
        }

        private IEnumerable<string> Open(string @from)
        {
            if(!texts.Contains(@from))
                throw new ArgumentException(string.Format("The text '{0}' was unknown.", @from),"from");

            Debug.Assert(LoremIpsums.ResourceManager != null, "LoremIpsums.ResourceManager != null");
            Debug.Assert(LoremIpsums.Culture != null, "LoremIpsums.Culture != null");

            string text = LoremIpsums.ResourceManager.GetString(@from, LoremIpsums.Culture);
            Debug.Assert(text != null, "text != null");

            return text.Split(new []{' '},StringSplitOptions.RemoveEmptyEntries);
        }
    }

}
