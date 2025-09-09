// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.Diagnostics;
using Microsoft.Build.Framework;

namespace NetTask
{
    public class ExampleTask : Microsoft.Build.Utilities.Task
    {
        public override bool Execute()
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var executingProcess = currentProcess.ProcessName;
                var processPath = currentProcess.MainModule?.FileName ?? "Unknown";

                Log.LogMessage(MessageImportance.High, $"The task is executed in process: {executingProcess}");
                Log.LogMessage(MessageImportance.High, $"Process path: {processPath}");

                return true;
            }
            catch (System.Exception ex)
            {
                Log.LogError($"Failed to determine executing process: {ex.Message}");
                return false;
            }
        }
    }
}
