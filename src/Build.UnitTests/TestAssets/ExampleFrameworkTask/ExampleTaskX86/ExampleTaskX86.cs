// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.Diagnostics;
using Microsoft.Build.Framework;

namespace SampleTask
{
    public class ExampleTaskX86 : Microsoft.Build.Utilities.Task
    {
        public string? PlatformTarget { get; set; }

        public override bool Execute()
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var executingProcess = currentProcess.ProcessName;
                var processPath = currentProcess.MainModule?.FileName ?? "Unknown";

                Log.LogMessage(MessageImportance.High, $"The task is executed in process: {executingProcess}");
                Log.LogMessage(MessageImportance.High, $"PlatformTarget: {PlatformTarget ?? "Not specified"}");

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
