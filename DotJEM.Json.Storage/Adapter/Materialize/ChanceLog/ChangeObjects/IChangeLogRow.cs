using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Storage.Adapter.Materialize.ChanceLog.ChangeObjects
{
    public interface IChangeLogRow : IDisposable
    {
        string Area { get; }
        long Generation { get; }

        int Size { get; }
        Guid Id { get; }
        ChangeType Type { get; }
        JObject CreateEntity();


        /// <summary>
        /// Opens a JsonReader for the data in the recorded change
        /// </summary>
        /// <remarks>
        /// When accessing the data on a ChangeLogRow through a reader, the resulting Object will not run though migration.
        /// </remarks>
        /// <returns></returns>
        JsonReader OpenReader();
    }
}