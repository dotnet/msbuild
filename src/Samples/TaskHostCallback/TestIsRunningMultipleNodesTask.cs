// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace TaskHostCallback;

/// <summary>
/// A simple task that tests the IsRunningMultipleNodes callback from TaskHost.
/// This task uses the CLR4 runtime and x86 architecture to force it to run in a TaskHost process.
/// </summary>
public class TestIsRunningMultipleNodesTask : Microsoft.Build.Utilities.Task
{
    [Output]
    public bool IsRunningMultipleNodes { get; set; }

    public override bool Execute()
    {
        // Access IBuildEngine2.IsRunningMultipleNodes - this should work in TaskHost
        // with our callback implementation
        if (BuildEngine is IBuildEngine2 buildEngine2)
        {
            IsRunningMultipleNodes = buildEngine2.IsRunningMultipleNodes;
            Log.LogMessage(MessageImportance.High, $"IsRunningMultipleNodes = {IsRunningMultipleNodes}");
            return true;
        }
        else
        {
            Log.LogError("BuildEngine does not implement IBuildEngine2");
            return false;
        }
    }
}
