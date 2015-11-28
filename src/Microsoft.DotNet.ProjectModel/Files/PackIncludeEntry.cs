// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.Extensions.JsonParser.Sources;

namespace Microsoft.DotNet.ProjectModel.Files
{
    public class PackIncludeEntry
    {
        public string Target { get; }
        public string[] SourceGlobs { get; }
        public int Line { get; }
        public int Column { get; }

        internal PackIncludeEntry(string target, JsonValue json)
            : this(target, ExtractValues(json), json.Line, json.Column)
        {
        }

        public PackIncludeEntry(string target, string[] sourceGlobs, int line, int column)
        {
            Target = target;
            SourceGlobs = sourceGlobs;
            Line = line;
            Column = column;
        }

        private static string[] ExtractValues(JsonValue json)
        {
            var valueAsString = json as JsonString;
            if (valueAsString != null)
            {
                return new string[] { valueAsString.Value };
            }

            var valueAsArray = json as JsonArray;
            if(valueAsArray != null)
            {
                return valueAsArray.Values.Select(v => v.ToString()).ToArray();
            }
            return new string[0];
        }
    }
}
