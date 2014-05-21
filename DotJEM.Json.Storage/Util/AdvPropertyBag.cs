using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace DotJEM.Json.Storage.Util
{
    /// <summary>
    /// <br/>Parameter Format Options:
    /// <br/> - f|format : Sets the format to use for the object property
    /// <br/> - a|align  : Sets the alignment to use for the format string
    /// <br/> - d|default : Sets the default value
    /// <br/> - f|type : Sets the type
    /// <br/>
    /// <br/>Type Options:
    /// <br/> - f|float : 128 bit Floating point value (decimal)
    /// <br/> - n|number : 64 bit Number (long)
    /// <br/> - d|date : DateTime
    /// <br/>
    /// <br/>if no type is set, string is presumed.
    /// </summary>
    /// <example>
    /// <br/> $(key) 
    /// <br/> $(key|format|default|type)
    /// <br/> $(key|format=000.000|default=50000|type=n)
    /// </example>
    public class AdvPropertyBag : IEnumerable<KeyValuePair<string, object>>
    {
        private const char ESCAPE = '\\';
        private readonly Dictionary<string, object> values = new Dictionary<string, object>();

        private readonly int pad;
        private readonly int endPad;
        private readonly int startPad;
        private readonly string startPattern;
        private readonly string endPattern;

        public int FormatIndex { get; set; }
        public int DefaultIndex { get; set; }
        public int TypeIndex { get; set; }

        /// <summary>
        /// Gets or Sets a value in the property bag.
        /// <br/> - Setting the value cals the <see cref="SetValue"/> method with the value and key provided.
        /// <br/> - Getting the value cals the <see cref="Format"/> method for the value stored for the key provided.
        /// </summary>
        /// <remarks>
        /// Because getting the value results in a call to <see cref="Format"/>, the value returned may not be the same as the value inserted, instead it is the
        /// resolved value, if value occurences is not present in the string they will be ignored and remain as occurenses.
        /// </remarks>
        public object this[string key]
        {
            get { return values[key]; }
            set { values[key] = value; }
        }

        /// <summary>
        /// Creates a new instance of the property bag.
        /// </summary>
        public AdvPropertyBag()
            : this("@(", ")")
        { }

        /// <summary>
        /// reates a new instance of the property bag with a custom start and end pattern.
        /// </summary>
        /// <param name="start">start pattern for a property, defualt is '@('</param>
        /// <param name="end">end pattern for a property, defualt is ')'</param>
        public AdvPropertyBag(string start, string end)
        {
            startPattern = start;
            endPattern = end;

            startPad = startPattern.Length;
            endPad = endPattern.Length;
            pad = startPad + endPad;
            FormatIndex = 1;
            DefaultIndex = 2;
            TypeIndex = 3;
        }

        /// <summary>
        /// Replaces all known occurences in the given string with values from the property bag.
        /// <br/>The format used for occurences of values is defined by the start and end pattern, default is '$(valueName)'.
        /// </summary>
        /// <param name="input">The string for which to replace all property value occurences.</param>
        /// <exception cref="ArgumentException">The input had a parameter that was not registered in the PropertyBag</exception>
        /// <remarks>
        /// Calling this method is the equivalent of calling <see cref="Format"/> with <paramref name="input"/> and 'throwsWhenMissingParameters' set to true.
        /// </remarks>
        public string Format(string input)
        {
            Parameter param;
            StringBuilder output = new StringBuilder(input);
            for (int i = 0; (param = RetreiveParameterInString(i, output)) != null; i = param.Start)
            {
                output = param.Replace(output, this);
            }
            return output.ToString();
        }

        private Parameter RetreiveParameterInString(int index, StringBuilder result)
        {
            string temp = result.ToString();

            int start = StartOfParameter(temp, index);
            if (start != -1)
            {
                int end = EndOfParameter(temp, start);
                if (end != -1)
                {
                    int len = end - start;
                    string name = temp.Substring(start, end - start);
                    string trimmed = name.Substring(startPad, len - pad);

                    Parameter param = new Parameter(trimmed, start, end, len, this);
                    if (values.ContainsKey(param.Key))
                    {
                        param.Value = values[param.Key];
                    }
                    return param;
                }
            }
            return null;
        }

        private int StartOfParameter(string result, int index)
        {
            while ((index = result.IndexOf(startPattern, index)) != -1)
            {
                if (index > 1 && result[index - 1] == ESCAPE)
                {
                    if (result[index - 2] != ESCAPE)
                    {
                        result.Remove(index - 1, 1);
                    }
                    else if (result[index - 2] == ESCAPE)
                    {
                        result.Remove(index - 1, 1);
                    }
                    index++;
                    continue;
                }
                if (index > 0 && result[index - 1] == ESCAPE)
                {
                    //result.Remove(index - 1, 1);
                    index++;
                    continue;
                }
                return index;
            }
            return -1;
        }

        private int EndOfParameter(string result, int index)
        {
            while ((index = result.IndexOf(endPattern, index)) != -1)
            {
                if (index > 0 && result[index - 1] == ESCAPE)
                    continue;
                return index+endPad;
            }
            return -1;
        }

        /// <summary>
        /// Sets a value in the property bag for the given key, the value is checked for references to it self, if such referece occures a exception is thrown.
        /// </summary>
        public AdvPropertyBag Add(string key, object value)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Cannot set value when Key is null or empty", "key");
            }

            if (value is string)
            {
                Parameter param;
                StringBuilder output = new StringBuilder(value.ToString());
                for (int i = 0; (param = RetreiveParameterInString(i, output)) != null; i = param.End)
                {
                    if (param.Key == key)
                    {
                        throw new ArgumentException("Value cannot contain a reference to it self");
                    }
                }
            }
            values[key] = value;
            return this;
        }

        public bool Contains(string key)
        {
            return values.ContainsKey(key);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return values.GetEnumerator();
        }

        private void ApplyConfiguration(Parameter param, string[] args)
        {
            string defaultType = string.Empty, defaultValue = string.Empty;
            for (int i = 1; i < args.Length; i++)
            {
                string[] config = args[i].Split('=');
                if (config.Length > 1)
                {
                    switch (config[0].ToLower())
                    {
                        case "f":
                        case "format":
                            param.Format = config[1];
                            break;
                        case "a":
                        case "align":
                            param.Align = int.Parse(config[1]);
                            break;
                        case "d":
                        case "default":
                            defaultValue = config[1];
                            break;
                        case "t":
                        case "type":
                            defaultType = config[1];
                            break;
                    }
                }
                else
                {
                    if (i == FormatIndex)
                        param.Format = config[0];
                    if (i == DefaultIndex)
                        param.Format = config[0];
                    if (i == TypeIndex)
                        param.Format = config[0];
                }
            }
            param.Default = ParseDefault(defaultType, defaultValue);
        }

        private static object ParseDefault(string type, string value)
        {
            switch (type.ToLower())
            {
                case "":
                    return value;
                case "f":
                case "float":
                    if (value == string.Empty)
                        return 0m;
                    return decimal.Parse(value);
                case "n":
                case "number":
                    if (value == string.Empty)
                        return 0;
                    return long.Parse(value);
                case "d":
                case "date":
                    if (value == string.Empty)
                        return DateTime.Now;
                    return DateTime.Parse(value);
                default:
                    throw new ArgumentException("Type '" + type + "' is unknown.");
            }
        }

        #region Nested type: Parameter

        private class Parameter
        {
            public int Start { get; private set; }
            public int End { get; private set; }

            public string Key { get; set; }
            public string Format { get; set; }

            public object Value { get; set; }
            public object Default { get; set; }

            public int Align { get; set; }
            public int Lenght { get; private set; }

            public Parameter(string key, int start, int end, int lenght, AdvPropertyBag bag)
            {
                Lenght = lenght;
                string[] args = key.Split('|');
                Key = args[0];
                End = end;
                Start = start;

                bag.ApplyConfiguration(this, args);
            }

            public StringBuilder Replace(StringBuilder param, AdvPropertyBag properties)
            {
                if (Value != null)
                {
                    return InternalReplace(param, properties, Value);
                }
                return InternalReplace(param, properties, Default);
            }

            private StringBuilder InternalReplace(StringBuilder param, AdvPropertyBag properties, object value)
            {
                if (value is string)
                {
                    value = properties.Format(value.ToString());
                }
                param.Remove(Start, Lenght);
                param.Insert(Start, string.Format(GennerateFormatString(), value));
                return param;
            }

            private string GennerateFormatString()
            {
                string format = "0";
                if (Align != 0) format += "," + Align;
                if (Format != string.Empty) format += ":" + Format; //TODO: Support more complex formats.
                return "{" + format + "}";
            }

            public override string ToString()
            {
                return Key;
            }
        }

        #endregion
    }
}