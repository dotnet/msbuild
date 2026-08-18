// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// A test task that reports its process ID and optionally calls BuildProjectFile
    /// on a child project. Used by TaskHost reuse E2E tests to verify that a parent
    /// task and a nested child task run in the same OOP TaskHost process.
    /// </summary>
    public class BuildProjectFileAndReportPidTask : Task
    {
        /// <summary>
        /// Path to the project file to build. If empty, just reports PID without building.
        /// </summary>
        public string ProjectFile { get; set; } = string.Empty;

        /// <summary>
        /// Semicolon-separated list of Property=Value pairs to pass as global properties.
        /// </summary>
        public string Properties { get; set; } = string.Empty;

        /// <summary>
        /// The process ID of the TaskHost running this task.
        /// </summary>
        [Output]
        public int ProcessId { get; set; }

        /// <summary>
        /// Whether the child build succeeded (only meaningful when ProjectFile is set).
        /// </summary>
        [Output]
        public bool BuildSucceeded { get; set; }

        public override bool Execute()
        {
            ProcessId = Process.GetCurrentProcess().Id;
            Log.LogMessage(MessageImportance.High, $"TASKHOST_PID={ProcessId}");

            if (!string.IsNullOrEmpty(ProjectFile))
            {
                Hashtable? globalProperties = null;
                if (!string.IsNullOrEmpty(Properties))
                {
                    globalProperties = new Hashtable();
                    foreach (string pair in Properties.Split(';'))
                    {
                        string[] parts = pair.Split(new[] { '=' }, 2);
                        if (parts.Length == 2)
                        {
                            globalProperties[parts[0].Trim()] = parts[1].Trim();
                        }
                    }
                }

                Hashtable targetOutputs = new();
                BuildSucceeded = BuildEngine.BuildProjectFile(ProjectFile, null, globalProperties, targetOutputs);
                Log.LogMessage(MessageImportance.High, $"BuildProjectFile({ProjectFile}) = {BuildSucceeded}");
            }
            else
            {
                BuildSucceeded = true;
            }

            return true;
        }
    }
}
