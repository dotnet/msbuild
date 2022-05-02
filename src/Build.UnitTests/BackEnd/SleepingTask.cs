// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Utilities;
using System.Threading;

namespace Microsoft.Build.UnitTests
{
    public class SleepingTask : Task
    {
        public int SleepTime { get; set; }

        /// <summary>
        /// Sleep for SleepTime milliseconds.
        /// </summary>
        /// <returns>Success on success.</returns>
        public override bool Execute()
        {
            // Thread.Sleep(SleepTime);
            System.Threading.Tasks.Task.Delay(SleepTime);
            return !Log.HasLoggedErrors;
        }
    }
}
