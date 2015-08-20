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
        private readonly IStorageContext context = new SqlServerStorageContext("Data Source=.\\DEV;Initial Catalog=json;Integrated Security=True");

        [Test, Explicit]
        public void Insert_Benchmark()
        {
            IStorageArea area = context.Area("simple");

            TestObjectGenerator generator = new TestObjectGenerator();

            //AutoResetEvent exit = new AutoResetEvent(false);
            //ThreadPool.RegisterWaitForSingleObject(exit, (o, b) => generator.Stop(), null, TimeSpan.FromMinutes(5), false);
            Stopwatch timer = Stopwatch.StartNew();

            IEnumerable<int> counts =
            generator.AsParallel().Select(doc =>
            {
                area.Insert(doc.ContentType, doc.Data);
                if (timer.ElapsedMilliseconds / (1000 * 60 * 5) > 0)
                {
                    generator.Stop();
                }
                return timer.ElapsedMilliseconds;
            }).GroupBy(ms => ms / 1000).OrderBy(g => g.Key).Select(group => group.Count()).ToList();

            //NOTE: 1000 Inserts pr. second, aint that ok?
            Assert.That(counts.Average(), Is.GreaterThan(1000));
        }

        [Test, Explicit]
        public void Get_Benchmarks()
        {
            IStorageArea area = context.Area("simple");

            var items = area.Get().Take(10);

            foreach (JObject jObject in items)
            {
                Console.WriteLine(jObject.ToString());
            }
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
        private RandomTextGenerator textGenerator = new RandomTextGenerator();

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
            string text = textGenerator.RandomText();
            //TODO: Bigger document and use contentype for propper stuff.
            return JObject.FromObject(new
            {
                Source = text,
                Content = textGenerator.Paragraph(text),
                Keys = textGenerator.Words(text, 4, 5)
            });
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

        public static T[] RandomItems<T>(this IEnumerable<T> items, int take)
        {
            ICollection<T> list = items as ICollection<T> ?? items.ToArray();
            return list.Skip(rand.Next(list.Count-take)).Take(take).ToArray();
        }

        public static IEnumerable<int> RandomSequence(int lenght, int maxValue, bool allowRepeats)
        {
            var sequence = RandomSequence(maxValue);
            if (!allowRepeats)
                sequence = sequence.Distinct();
            return sequence.Take(lenght);
        }

        private static IEnumerable<int> RandomSequence(int maxValue)
        {
            while (true) yield return rand.Next(maxValue);
        }
    }

    public class RandomTextGenerator
    {
        private readonly string[] texts = "Childharold,Decameron,Faust,Inderfremde,Lebateauivre,Lemasque,Loremipsum,Nagyonfaj,Omagyar,Robinsonokruso,Theraven,Tierrayluna".Split(',');

        public string RandomText()
        {
            return texts.RandomItem();
        }

        public string Paragraph(string @from, int count = 20)
        {
            return Open(from).RandomItems(count).Aggregate((s, s1) => s + " " + s1);
        }

        public string Word(string @from, int minLength = 2)
        {
            return Open(from).Where(w => w.Length >= minLength).RandomItem();
        }

        private IEnumerable<string> Open(string @from)
        {
            if(!texts.Contains(@from))
                throw new ArgumentException(string.Format("The text '{0}' was unknown.", @from),"from");

            Debug.Assert(LoremIpsums.ResourceManager != null, "LoremIpsums.ResourceManager != null");

            string text = LoremIpsums.ResourceManager.GetString(@from, LoremIpsums.Culture);
            Debug.Assert(text != null, "text != null");

            return text.Split(new []{' '},StringSplitOptions.RemoveEmptyEntries);
        }

        public string[] Words(string @from, int minLength = 2, int count = 20)
        {
            HashSet<string> unique = new HashSet<string>(Open(from).Where(w => w.Length >= minLength));
            return Enumerable.Repeat("", count)
                .Select(s => unique.RandomItem())
                .ToArray();
        }
    }

}
