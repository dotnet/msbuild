// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// A test task that exercises IBuildEngine3 Yield/Reacquire callbacks.
    /// Used to test that yield/reacquire callbacks work correctly in the task host.
    /// </summary>
    public class YieldReacquireTask : Task
    {
        /// <summary>
        /// If true, calls Yield() then Reacquire().
        /// </summary>
        public bool PerformYieldReacquire { get; set; } = true;

        /// <summary>
        /// Output: True if the task completed without exceptions.
        /// </summary>
        [Output]
        public bool CompletedSuccessfully { get; set; }

        /// <summary>
        /// Output: True if Yield was called successfully.
        /// </summary>
        [Output]
        public bool YieldCalled { get; set; }

        /// <summary>
        /// Output: True if Reacquire was called successfully.
        /// </summary>
        [Output]
        public bool ReacquireCalled { get; set; }

        /// <summary>
        /// Output: Exception message if an error occurred.
        /// </summary>
        [Output]
        public string ErrorMessage { get; set; }

        public override bool Execute()
        {
            try
            {
                if (BuildEngine is IBuildEngine3 engine3)
                {
                    if (PerformYieldReacquire)
                    {
                        // Yield - this is non-blocking
                        engine3.Yield();
                        YieldCalled = true;
                        Log.LogMessage(MessageImportance.High, "YieldReacquireTask: Yield() called successfully");

                        // Simulate some work while yielded
                        System.Threading.Thread.Sleep(1000);

                        // Reacquire - this blocks until scheduler allows us to continue
                        engine3.Reacquire();
                        ReacquireCalled = true;
                        Log.LogMessage(MessageImportance.High, "YieldReacquireTask: Reacquire() called successfully");
                    }

                    CompletedSuccessfully = true;
                    return true;
                }

                Log.LogError("BuildEngine does not implement IBuildEngine3");
                ErrorMessage = "BuildEngine does not implement IBuildEngine3";
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
