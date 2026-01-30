// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// A test task that exercises IBuildEngine9 RequestCores/ReleaseCores callbacks.
    /// Used to test that resource management callbacks work correctly in the task host.
    /// </summary>
    public class ResourceManagementTask : Task
    {
        /// <summary>
        /// Number of cores to request. Defaults to 2.
        /// </summary>
        public int RequestedCores { get; set; } = 2;

        /// <summary>
        /// If true, releases the cores after requesting them.
        /// </summary>
        public bool ReleaseCoresAfterRequest { get; set; } = true;

        /// <summary>
        /// Output: Number of cores actually granted by the scheduler.
        /// </summary>
        [Output]
        public int CoresGranted { get; set; }

        /// <summary>
        /// Output: True if the task completed without exceptions.
        /// </summary>
        [Output]
        public bool CompletedSuccessfully { get; set; }

        /// <summary>
        /// Output: Exception message if an error occurred.
        /// </summary>
        [Output]
        public string ErrorMessage { get; set; }

        public override bool Execute()
        {
            try
            {
                if (BuildEngine is IBuildEngine9 engine9)
                {
                    // Request cores
                    CoresGranted = engine9.RequestCores(RequestedCores);
                    Log.LogMessage(MessageImportance.High,
                        $"ResourceManagement: Requested {RequestedCores} cores, granted {CoresGranted}");

                    // Release cores if requested
                    if (ReleaseCoresAfterRequest && CoresGranted > 0)
                    {
                        engine9.ReleaseCores(CoresGranted);
                        Log.LogMessage(MessageImportance.High,
                            $"ResourceManagement: Released {CoresGranted} cores");
                    }

                    CompletedSuccessfully = true;
                    return true;
                }

                Log.LogError("BuildEngine does not implement IBuildEngine9");
                ErrorMessage = "BuildEngine does not implement IBuildEngine9";
                return false;
            }
            catch (System.Exception ex)
            {
                Log.LogErrorFromException(ex);
                ErrorMessage = $"{ex.GetType().Name}: {ex.Message}";
                CompletedSuccessfully = false;
                return false;
            }
        }
    }
}
