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

        /// <summary>
        /// Creates a TaskEnvironment backed by the multi-threaded driver which virtualizes
        /// environment variables and current directory. This allows testing of multithreaded mode
        /// behavior where each project has its own isolated environment.
        /// </summary>
        /// <param name="projectDirectory">The project directory to use for the task environment.</param>
        /// <returns>A TaskEnvironment suitable for testing multithreaded mode scenarios.</returns>
        /// <remarks>
        /// The caller is responsible for disposing the TaskEnvironment via TaskEnvironment.Dispose(),
        /// which will clean up the underlying driver's thread-local state.
        /// </remarks>
        // CA2000 is suppressed because the driver is owned by the TaskEnvironment and disposed via TaskEnvironment.Dispose()
#pragma warning disable CA2000
        public static TaskEnvironment CreateMultithreadedForTest(string projectDirectory)
        {
            return new TaskEnvironment(new MultiThreadedTaskEnvironmentDriver(projectDirectory));
        }
#pragma warning restore CA2000
    }
}
