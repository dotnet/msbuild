// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// A simple task that queries global properties from the build engine via IBuildEngine6.
    /// Used by TaskHostCallback_Tests to verify that request-level global properties
    /// (not just build-level properties) are forwarded through TaskHostTask to the out-of-proc TaskHost.
    /// </summary>
    public class GetGlobalPropertiesTask : Task
    {
        [Output]
        public int GlobalPropertyCount { get; set; }

        public override bool Execute()
        {
            if (BuildEngine is IBuildEngine6 engine6)
            {
                IReadOnlyDictionary<string, string> globalProperties = engine6.GetGlobalProperties();
                GlobalPropertyCount = globalProperties.Count;

                foreach (KeyValuePair<string, string> kvp in globalProperties)
                {
                    Log.LogMessage(MessageImportance.High, $"GlobalProperty: {kvp.Key}={kvp.Value}");
                }

                Log.LogMessage(MessageImportance.High, $"GlobalPropertyCount = {GlobalPropertyCount}");
                return true;
            }

            Log.LogError("BuildEngine does not implement IBuildEngine6");
            return false;
        }
    }
}
