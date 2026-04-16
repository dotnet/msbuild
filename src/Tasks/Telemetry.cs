// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Task that logs telemetry.
    /// </summary>
    public sealed class Telemetry : TaskExtension
    {
        /// <summary>
        /// Gets or sets a semi-colon delimited list of equal-sign separated key/value pairs.  An example would be &quot;Property1=Value1;Property2=Value2&quot;.
        /// </summary>
        public string EventData { get; set; }

        /// <summary>
        /// Gets or sets the event name.
        /// </summary>
        [Required]
        public string EventName { get; set; }

        /// <summary>
        /// Main task method
        /// </summary>
        public override bool Execute()
        {
            IDictionary<string, string> properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!String.IsNullOrEmpty(EventData))
            {
                foreach (string pair in EventData.Split(MSBuildConstants.SemicolonChar, StringSplitOptions.RemoveEmptyEntries))
                {
                    var item = pair.Split(MSBuildConstants.EqualsChar, 2, StringSplitOptions.RemoveEmptyEntries);

                    if (item.Length != 2)
                    {
                        Log.LogMessageFromResources(MessageImportance.Low, "Telemetry.IllegalEventDataString", pair, EventData);
                        return false;
                    }

                    // Last value added wins
                    //
                    properties[item[0]] = item[1];
                }
            }

            Log.LogTelemetry(EventName, properties);

            return !Log.HasLoggedErrors;
        }
    }
}
