﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotJEM.Json.Storage.Migration
{
    public interface IVersionProvider : IComparer<string>
    {
        string Current { get; }
    }
}
