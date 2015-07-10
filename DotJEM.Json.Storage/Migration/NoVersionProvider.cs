using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotJEM.Json.Storage.Migration
{
    public class NoVersionProvider : IVersionProvider
    {
        public string Current
        {
            get { return ""; }
        }

        public int Compare(string version1, string version2)
        {
            return 0;
        }
    }
}
