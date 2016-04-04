// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.ProjectModel.Files
{
    public class PackIncludeEntry
    {
        public string Target { get; }
        public string[] SourceGlobs { get; }
        public int Line { get; }
        public int Column { get; }

        internal PackIncludeEntry(string target, JToken json)
        {
            Target = target;
            SourceGlobs = ExtractValues(json);

            var lineInfo = (IJsonLineInfo)json;
            Line = lineInfo.LineNumber;
            Column = lineInfo.LinePosition;
        }

        public PackIncludeEntry(string target, string[] sourceGlobs, int line, int column)
        {
            Target = target;
            SourceGlobs = sourceGlobs;
            Line = line;
            Column = column;
        }

        private static string[] ExtractValues(JToken json)
        {
            if (json.Type == JTokenType.String)
            {
                return new string[] { json.Value<string>() };
            }

            if(json.Type == JTokenType.Array)
            {
                return json.Select(v => v.ToString()).ToArray();
            }
            return new string[0];
        }
    }
}
