using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotJEM.Json.Storage.Migration
{
    public interface IVersionProvider
    {
        string Current { get; }
        int Compare(string version1, string version2);
    }
}
