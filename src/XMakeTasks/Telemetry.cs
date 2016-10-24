using Microsoft.Build.Framework;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Build.Tasks
{
    public sealed class Telemetry : TaskExtension
    {
        [Required]
        public string EventName { get; set; }

        public string EventData { get; set; }

        public override bool Execute()
        {
            // TODO: Error checking

            IDictionary<string, string> properties = new Dictionary<string, string>();

            if (!String.IsNullOrEmpty(EventData))
            {
                foreach (string pair in EventData.Split(new[] {';'}, StringSplitOptions.RemoveEmptyEntries))
                {
                    var item = pair.Split(new[] {'='}, 2, StringSplitOptions.RemoveEmptyEntries);

                    properties.Add(item[0], item[1]);
                }
            }

            Log.LogTelemetry(EventName, properties);

            return true;
        }
    }
}