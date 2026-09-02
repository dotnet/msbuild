// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Framework;

namespace NetTask
{
    public class ExampleTask : Microsoft.Build.Utilities.Task
    {
        public enum CopyMode
        {
            Shallow,
            Deep,
        }

        // nullable isn't available in net framework runtime
        // the presence of the property covers the test case
        public string? OutputValue { get; set; }

        public CopyMode Mode { get; set; }

        public CopyMode[]? Modes { get; set; }

        public FileInfo? DestinationFile { get; set; }

        public FileInfo[]? DestinationFiles { get; set; }

        public override bool Execute()
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var executingProcess = currentProcess.ProcessName;
                var processPath = currentProcess.MainModule?.FileName ?? "Unknown";

                Log.LogMessage(MessageImportance.High, $"The task is executed in process: {executingProcess} with id {currentProcess.Id}");
                Log.LogMessage(MessageImportance.High, $"Process path: {processPath}");

                string[] args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length; i++)
                {
                    Log.LogMessage(MessageImportance.High, $"Arg[{i}]: {args[i]}");
                }

                if (DestinationFile is not null)
                {
                    Log.LogMessage(
                        MessageImportance.High,
                        $"PARAMETER_BINDING_OK Mode={Mode} Modes={string.Join(",", Modes ?? [])} DestinationFile={DestinationFile.FullName} DestinationFiles={string.Join(",", Array.ConvertAll(DestinationFiles ?? [], file => file.FullName))}");
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
