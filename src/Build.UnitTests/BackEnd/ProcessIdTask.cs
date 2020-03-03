// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Utilities;
using System;
using System.Diagnostics;
namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// This task was created for https://github.com/microsoft/msbuild/issues/3141
    /// </summary>
    public class ProcessIdTask : Task
    {
        /// <summary>
        /// Log the id for this process.
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            Log.LogMessage("PID to shut down is " + Process.GetCurrentProcess().Id + " (EndPID)");
            return true;
        }
    }
}
