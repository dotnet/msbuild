// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Helper class for creating TaskEnvironment instances in tests.
    /// </summary>
    public static class TaskEnvironmentHelper
    {
        /// <summary>
        /// Creates a TaskEnvironment backed by the multi-process driver which directly
        /// uses the process environment and working directory.
        /// </summary>
        /// <returns>A TaskEnvironment suitable for use in tests.</returns>
        public static TaskEnvironment CreateForTest()
        {
            return new TaskEnvironment(MultiProcessTaskEnvironmentDriver.Instance);
        }
    }
}
