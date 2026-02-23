// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace NetTask
{
    /// <summary>
    /// A simple task that queries IsRunningMultipleNodes via IBuildEngine2 callback.
    /// Used by NetTaskHost_E2E_Tests to test callbacks in the .NET Core TaskHost
    /// spawned from a .NET Framework parent.
    /// Functionally identical to IsRunningMultipleNodesTask in the test DLL, but must
    /// be a separate assembly targeting .NET Core for cross-runtime E2E testing.
    /// </summary>
    public class CallbackTestTask : Microsoft.Build.Utilities.Task
    {
        [Output]
        public bool IsRunningMultipleNodes { get; set; }

        public override bool Execute()
        {
            if (BuildEngine is IBuildEngine2 engine2)
            {
                IsRunningMultipleNodes = engine2.IsRunningMultipleNodes;
                Log.LogMessage(MessageImportance.High, $"IsRunningMultipleNodes = {IsRunningMultipleNodes}");
                return true;
            }

            Log.LogError("BuildEngine does not implement IBuildEngine2");
            return false;
        }
    }
}
