using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Extensions.ProjectModel
{
    public class PackIncludeEntry
    {
        public string Target { get; }
        public string[] SourceGlobs { get; }
        public int Line { get; }
        public int Column { get; }

        internal PackIncludeEntry(string target, JToken json)
            : this(target, ExtractValues(json), ((IJsonLineInfo)json).LineNumber, ((IJsonLineInfo)json).LinePosition)
        {
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
                return json.Value<JArray>().Select(v => v.ToString()).ToArray();
            }
            return new string[0];
        }
    }
}