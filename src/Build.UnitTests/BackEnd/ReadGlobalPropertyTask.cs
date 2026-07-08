// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Reads a global property (via <see cref="IBuildEngine6.GetGlobalProperties"/>) from inside the (possibly
    /// out-of-proc) task execution process and returns its value together with the executing process id. Used to
    /// verify that an out-of-proc task host reconstructs the global properties correctly across consecutive tasks,
    /// including ones whose global-properties configuration was sent as the deduplicated "identical" marker.
    /// </summary>
    public class ReadGlobalPropertyTask : Task
    {
        [Required]
        public string PropertyName { get; set; } = string.Empty;

        [Output]
        public string Value { get; set; } = string.Empty;

        [Output]
        public int Pid { get; set; }

        public override bool Execute()
        {
            Pid = Process.GetCurrentProcess().Id;

            if (BuildEngine is IBuildEngine6 buildEngine6)
            {
                IReadOnlyDictionary<string, string> globalProperties = buildEngine6.GetGlobalProperties();
                if (globalProperties.TryGetValue(PropertyName, out string? value) && value is not null)
                {
                    Value = value;
                }
            }

            return true;
        }
    }
}
