using System;
using System.Collections;
using System.Collections.Generic;
using DotJEM.Json.Storage.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Adapter.Materialize.ChanceLog.ChangeObjects
{


    public class CreateChangeLogRow : ChangeLogEntityRow
    {
        private readonly byte[] data;
        public override ChangeType Type => ChangeType.Create;
        public override int Size => data.Length;

        public CreateChangeLogRow(IStorageContext context, string area, long token, Guid id, string contentType, long reference, int version, DateTime created, DateTime updated,
            byte[] data) : base(context, area, token, id, contentType,reference, version, created, updated)
        {
            this.data = data;
        }

        public override JObject CreateEntity() => AttachProperties(Serializer.Deserialize(data));
        public override JsonReader OpenReader() => new InjectingJsonReader(Serializer.OpenReader(data), new ChangeLogEntityRowInjector(Context, this));
    }

    internal static  class RowMappers
    {
        internal static readonly Func<IChangeLogRow, (JsonToken, object)> ID_MAPPER = row => (JsonToken.String, row.Id.ToString());
        internal static readonly Func<ChangeLogEntityRow, (JsonToken, object)> REFERENCE_MAPPER = row => (JsonToken.String, Base36.Encode(row.Reference));
        internal static readonly Func<ChangeLogEntityRow, (JsonToken, object)> AREA_MAPPER = row => (JsonToken.String, row.Area);
        internal static readonly Func<ChangeLogEntityRow, (JsonToken, object)> VERSION_MAPPER = row => (JsonToken.Integer, row.Version);
        internal static readonly Func<ChangeLogEntityRow, (JsonToken, object)> CONTENT_TYPE_MAPPER = row => (JsonToken.String, row.ContentType);
        internal static readonly Func<ChangeLogEntityRow, (JsonToken, object)> CREATED_MAPPER = row => (JsonToken.Date, DateTime.SpecifyKind(row.Created, DateTimeKind.Utc));
        internal static readonly Func<ChangeLogEntityRow, (JsonToken, object)> UPDATED_MAPPER = row => (JsonToken.Date, DateTime.SpecifyKind(row.Updated, DateTimeKind.Utc));
    }

    public class ChangeLogEntityRowInjector : IInjectingJsonReaderValues
    {
        private readonly ChangeLogEntityRow row;
       
        private readonly Dictionary<string, Func<ChangeLogEntityRow, (JsonToken, object)>> mappers
            = new Dictionary<string, Func<ChangeLogEntityRow, (JsonToken, object)>>();

        public int Count => mappers.Count;

        public ChangeLogEntityRowInjector(IStorageContext context, ChangeLogEntityRow row)
        {
            this.row = row;
            mappers[context.Configuration.Fields[JsonField.Id]] = RowMappers.ID_MAPPER;
            mappers[context.Configuration.Fields[JsonField.Reference]] = RowMappers.REFERENCE_MAPPER;
            mappers[context.Configuration.Fields[JsonField.Area]] = RowMappers.AREA_MAPPER;
            mappers[context.Configuration.Fields[JsonField.Version]] = RowMappers.VERSION_MAPPER;
            mappers[context.Configuration.Fields[JsonField.ContentType]] = RowMappers.CONTENT_TYPE_MAPPER;
            mappers[context.Configuration.Fields[JsonField.Created]] = RowMappers.CREATED_MAPPER;
            mappers[context.Configuration.Fields[JsonField.Updated]] = RowMappers.UPDATED_MAPPER;
        }

        public IEnumerator<(string, (JsonToken, object))> GetEnumerator()
        {
            foreach (KeyValuePair<string, Func<ChangeLogEntityRow, (JsonToken, object)>> pair in mappers)
                yield return (pair.Key, pair.Value(row));
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool TryGetValue(string key, out (JsonToken, object) value)
        {
            if (mappers.TryGetValue(key, out Func<ChangeLogEntityRow, (JsonToken, object)> func))
            {
                value = func(row);
                return true;
            }

            value = default;
            return false;
        }

        public bool TryRemoveValue(string key, out (JsonToken, object) value)
        {
            return TryGetValue(key, out value) && mappers.Remove(key);
        }

        public void Clear()
        {
            mappers.Clear();
        }
    }

    public interface IInjectingJsonReaderValues : IEnumerable<(string, (JsonToken, object))>
    {
        int Count { get; }
        bool TryGetValue(string key, out (JsonToken, object) value);

        bool TryRemoveValue(string key, out (JsonToken, object) value);
        void Clear();
    }

    class NullInjectingJsonReaderValues : IInjectingJsonReaderValues
    {
        public static IInjectingJsonReaderValues Default { get; } = new NullInjectingJsonReaderValues();

        public int Count { get; } = 0;

        public IEnumerator<(string, (JsonToken, object))> GetEnumerator()
        {
            yield break;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool TryGetValue(string key, out (JsonToken, object) value)
        {
            value = default;
            return false;
        }

        public bool TryRemoveValue(string key, out (JsonToken, object) value)
        {
            value = default;
            return false;
        }

        public void Clear()
        {
        }
    }


    /// <summary>
    /// Json Reader that can inject or override properties at the root level.
    /// </summary>
    public class InjectingJsonReader : JsonReader
    {
        private readonly JsonReader innerReader;
        private readonly IInjectingJsonReaderValues inject;
        private readonly Queue<(JsonToken, object)> queue = new Queue<(JsonToken, object)>();
        private readonly Func<(JsonToken, object)> defaultGetValue;

        private Func<(JsonToken, object)> getValue;
        private int startObjects = 0;

        public InjectingJsonReader(JsonReader innerReader, IInjectingJsonReaderValues inject = null)
        {
            this.innerReader = innerReader;
            this.inject = inject ?? NullInjectingJsonReaderValues.Default;
            this.getValue = this.defaultGetValue = () => (innerReader.TokenType, innerReader.Value);
        }

        private (JsonToken, object) GetValue()
        {
            (JsonToken, object) value = this.getValue();
            this.getValue = defaultGetValue;
            return value;
        }

        private bool Dequeue()
        {
            (JsonToken type, object value) = queue.Dequeue();
            SetToken(type, value);
            return true;
        }

        public override bool Read()
        {
            if (queue.Count > 0) return Dequeue();
            bool state = innerReader.Read();
            switch (innerReader.TokenType)
            {
                case JsonToken.StartObject:
                    startObjects++;
                    SetToken(innerReader.TokenType, innerReader.Value);
                    return true;

                case JsonToken.EndObject:
                    startObjects--;
                    if (startObjects > 0 || inject.Count < 1)
                    {
                        SetToken(innerReader.TokenType, innerReader.Value);
                        return true;
                    }
                    EmptyInjectionsIntoQueue();
                    return Dequeue();

                case JsonToken.PropertyName:
                    SetToken(innerReader.TokenType, innerReader.Value);
                    if (inject.TryRemoveValue((string)innerReader.Value, out var val))
                    {
                        getValue = () => val;
                    }
                    return state;

                default:
                    (JsonToken type, object value) = GetValue();
                    SetToken(type, value);
                    return state;
            }
        }

        private void EmptyInjectionsIntoQueue()
        {
            foreach ((string key, (JsonToken, object) value) in this.inject)
            {
                queue.Enqueue((JsonToken.PropertyName, key));
                queue.Enqueue(value);
            }

            queue.Enqueue((innerReader.TokenType, innerReader.Value));
            inject.Clear();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ((IDisposable)innerReader).Dispose();
            }
            base.Dispose(disposing);
        }
    }

}