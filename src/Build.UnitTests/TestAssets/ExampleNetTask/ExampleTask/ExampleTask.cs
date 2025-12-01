// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System;
using System.Diagnostics;
using Microsoft.Build.Framework;

namespace NetTask
{
    public class ExampleTask : Microsoft.Build.Utilities.Task
    {
        // nullable isn't available in net framework runtime
        // the presence of the property covers the test case
        public string? OutputValue { get; set; }

        public override bool Execute()
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var executingProcess = currentProcess.ProcessName;
                var processPath = currentProcess.MainModule?.FileName ?? "Unknown";

                Log.LogMessage(MessageImportance.High, $"The task is executed in process: {executingProcess}");
                Log.LogMessage(MessageImportance.High, $"Process path: {processPath}");

                string[] args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length; i++)
                {
                    Log.LogMessage(MessageImportance.High, $"Arg[{i}]: {args[i]}");
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to determine executing process: {ex.Message}");
                return false;
            }
        }
    }
}
