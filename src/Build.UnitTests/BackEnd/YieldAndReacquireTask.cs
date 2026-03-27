// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// A test task that explicitly calls IBuildEngine3.Yield() and Reacquire()
    /// to verify the yield/reacquire callback flow through the OOP TaskHost.
    /// </summary>
    public class YieldAndReacquireTask : Task
    {
        /// <summary>
        /// Whether the yield/reacquire round-trip succeeded.
        /// </summary>
        [Output]
        public bool YieldSucceeded { get; set; }

        public override bool Execute()
        {
            if (BuildEngine is not IBuildEngine3 engine3)
            {
                Log.LogError("IBuildEngine3 not available — cannot test Yield/Reacquire.");
                return false;
            }

            Log.LogMessage(MessageImportance.High, "YieldAndReacquireTask: calling Yield()");
            engine3.Yield();

            Log.LogMessage(MessageImportance.High, "YieldAndReacquireTask: calling Reacquire()");
            engine3.Reacquire();

            Log.LogMessage(MessageImportance.High, "YieldAndReacquireTask: Yield/Reacquire round-trip completed successfully.");
            YieldSucceeded = true;
            return true;
        }
    }
}
