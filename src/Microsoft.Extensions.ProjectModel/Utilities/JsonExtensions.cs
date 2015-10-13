using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Newtonsoft.Json.Linq
{
    internal static class JsonExtensions
    {
        public static string[] ValueAsStringArray(this JObject self, string property)
        {
            var prop = self.Property(property);
            if (prop != null)
            {
                if (prop.Value.Type == JTokenType.String)
                {
                    return new string[] { prop.Value.ToString() };
                }
                else if (prop.Value.Type == JTokenType.Array)
                {
                    return ((JArray)prop.Value).Select(t => t.ToString()).ToArray();
                }
            }
            return new string[0];
        }
    }
}
