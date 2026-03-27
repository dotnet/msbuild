// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// A test task that performs a configurable callback operation:
    /// - "Yield": calls Yield() then Reacquire()
    /// - "BuildProjectFile": calls BuildProjectFile on a child project
    /// Reports its identity and result for verification in nested/concurrent scenarios.
    /// </summary>
    public class ConfigurableCallbackTask : Task
    {
        /// <summary>
        /// The callback operation to perform: "Yield" or "BuildProjectFile".
        /// </summary>
        [Required]
        public string Operation { get; set; } = "Yield";

        /// <summary>
        /// An identifier for this task instance, used in log messages for verification.
        /// </summary>
        [Required]
        public string TaskIdentity { get; set; } = string.Empty;

        /// <summary>
        /// Path to the child project file (required when Operation is "BuildProjectFile").
        /// </summary>
        public string ChildProjectFile { get; set; } = string.Empty;

        /// <summary>
        /// Whether the operation succeeded.
        /// </summary>
        [Output]
        public bool OperationSucceeded { get; set; }

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, $"ConfigurableCallbackTask [{TaskIdentity}]: starting operation '{Operation}'");

            switch (Operation)
            {
                case "Yield":
                    if (BuildEngine is not IBuildEngine3 engine3)
                    {
                        Log.LogError($"[{TaskIdentity}] IBuildEngine3 not available for Yield.");
                        return false;
                    }

                    engine3.Yield();
                    engine3.Reacquire();
                    OperationSucceeded = true;
                    break;

                case "BuildProjectFile":
                    if (string.IsNullOrEmpty(ChildProjectFile))
                    {
                        Log.LogError($"[{TaskIdentity}] ChildProjectFile is required for BuildProjectFile operation.");
                        return false;
                    }

                    IDictionary targetOutputs = new Dictionary<string, object>();
                    bool success = BuildEngine.BuildProjectFile(ChildProjectFile, null, null, targetOutputs);
                    OperationSucceeded = success;
                    break;

                default:
                    Log.LogError($"[{TaskIdentity}] Unknown operation: {Operation}");
                    return false;
            }

            Log.LogMessage(MessageImportance.High, $"ConfigurableCallbackTask [{TaskIdentity}]: operation '{Operation}' completed, success={OperationSucceeded}");
            return true;
        }
    }
}
