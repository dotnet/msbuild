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

    /// <summary>
    /// If true, also tests RequestCores/ReleaseCores (IBuildEngine9).
    /// </summary>
    public bool TestResourceManagement { get; set; }

    /// <summary>
    /// Number of cores granted by RequestCores (only valid if TestResourceManagement is true).
    /// </summary>
    [Output]
    public int CoresGranted { get; set; }

    public override bool Execute()
    {
        // Access IBuildEngine2.IsRunningMultipleNodes - this should work in TaskHost
        // with our callback implementation
        if (BuildEngine is IBuildEngine2 buildEngine2)
        {
            IsRunningMultipleNodes = buildEngine2.IsRunningMultipleNodes;
            Log.LogMessage(MessageImportance.High, $"IsRunningMultipleNodes = {IsRunningMultipleNodes}");
        }
        else
        {
            Log.LogError("BuildEngine does not implement IBuildEngine2");
            return false;
        }

        // Test resource management if requested
        if (TestResourceManagement)
        {
            if (BuildEngine is IBuildEngine9 buildEngine9)
            {
                // Request 4 cores
                CoresGranted = buildEngine9.RequestCores(4);
                Log.LogMessage(MessageImportance.High, $"RequestCores(4) returned: {CoresGranted}");

                // Release them
                buildEngine9.ReleaseCores(CoresGranted);
                Log.LogMessage(MessageImportance.High, $"ReleaseCores({CoresGranted}) completed successfully");
            }
            else
            {
                Log.LogError("BuildEngine does not implement IBuildEngine9");
                return false;
            }
        }

        return true;
    }
}
