// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Diagnostics;
namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// This task was created for https://github.com/microsoft/msbuild/issues/3141
    /// </summary>
    public class ProcessIdTask : Task
    {
        [Output]
        public int Pid { get; set; }

        /// <summary>
        /// Log the id for this process.
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            Pid = Process.GetCurrentProcess().Id;
            return true;
        }
    }
}
