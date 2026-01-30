// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// A test task that exercises multiple IBuildEngine callbacks in sequence.
    /// Used to test that callback request/response correlation works correctly.
    /// </summary>
    public class MultipleCallbackTask : Task
    {
        /// <summary>
        /// Number of times to call RequestCores/ReleaseCores in a loop.
        /// </summary>
        public int Iterations { get; set; } = 5;

        /// <summary>
        /// Output: IsRunningMultipleNodes value from first query.
        /// </summary>
        [Output]
        public bool IsRunningMultipleNodes { get; set; }

        /// <summary>
        /// Output: Total cores granted across all iterations.
        /// </summary>
        [Output]
        public int TotalCoresGranted { get; set; }

        /// <summary>
        /// Output: Number of successful callback round-trips.
        /// </summary>
        [Output]
        public int SuccessfulCallbacks { get; set; }

        public override bool Execute()
        {
            try
            {
                // Test IsRunningMultipleNodes
                if (BuildEngine is IBuildEngine2 engine2)
                {
                    IsRunningMultipleNodes = engine2.IsRunningMultipleNodes;
                    SuccessfulCallbacks++;
                    Log.LogMessage(MessageImportance.High,
                        $"IsRunningMultipleNodes = {IsRunningMultipleNodes}");
                }

                // Test RequestCores/ReleaseCores multiple times
                if (BuildEngine is IBuildEngine9 engine9)
                {
                    for (int i = 0; i < Iterations; i++)
                    {
                        int granted = engine9.RequestCores(1);
                        TotalCoresGranted += granted;
                        SuccessfulCallbacks++;

                        if (granted > 0)
                        {
                            engine9.ReleaseCores(granted);
                            SuccessfulCallbacks++;
                        }

                        Log.LogMessage(MessageImportance.Normal,
                            $"Iteration {i + 1}: Requested 1 core, granted {granted}");
                    }
                }

                Log.LogMessage(MessageImportance.High,
                    $"MultipleCallbackTask completed: {SuccessfulCallbacks} successful callbacks");
                return true;
            }
            catch (System.Exception ex)
            {
                Log.LogErrorFromException(ex);
                return false;
            }
        }
    }
}
