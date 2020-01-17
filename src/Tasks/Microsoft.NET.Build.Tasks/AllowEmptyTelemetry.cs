using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.NET.Build.Tasks;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Task that logs telemetry.
    /// </summary>
    public sealed class AllowEmptyTelemetry : TaskBase
    {
        /// <summary>
        /// Gets or sets a semi-colon delimited list of equal-sign separated key/value pairs.  An example would be &quot;Property1=Value1;Property2=Value2&quot;Property3=.
        /// Property3's value will be "null"
        /// </summary>
        public string EventData { get; set; }

        [Required] public string EventName { get; set; }

        protected override void ExecuteCore()
        {
            IDictionary<string, string> properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(EventData))
            {
                foreach (string pair in EventData.Split(new[] {';'}, StringSplitOptions.RemoveEmptyEntries))
                {
                    var item = pair.Split(new[] {'='}, 2);

                    if (string.IsNullOrWhiteSpace(item[0]))
                    {
                        throw new ArgumentException($"{pair} is invalid.");
                    }

                    if (item.Length < 2 || string.IsNullOrWhiteSpace(item[1]))
                    {
                        properties[item[0]] = "null";
                    }
                    else
                    {
                        properties[item[0]] = item[1];
                    }
                }
            }

            (BuildEngine as IBuildEngine5)?.LogTelemetry(EventName, properties);
        }
    }
}
