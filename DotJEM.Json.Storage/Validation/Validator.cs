using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DotJEM.Json.Storage.Validation
{
    internal static class Validator
    {
        private static readonly Regex areaValidator = new Regex("^(\\w|[_\\-.])+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static void ValidateArea(string name)
        {
            if(!areaValidator.IsMatch(name))
                throw new ArgumentException("The name for an area must consist of alfanumeric characters and/or '-', '_' and '.'.");
        }
    }
}
