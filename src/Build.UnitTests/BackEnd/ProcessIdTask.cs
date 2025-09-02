// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
#nullable disable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// This task was created for https://github.com/dotnet/msbuild/issues/3141
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
