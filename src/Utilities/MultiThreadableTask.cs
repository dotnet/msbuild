// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Utilities
{
    /// <summary>
    /// Abstract base class for multithreadable tasks that provides default implementations
    /// for the IMultiThreadableTask interface. Task authors only need to implement the
    /// Execute method from ITask and use the TaskEnvironment property for thread-safe
    /// operations.
    /// </summary>
    public abstract class MultiThreadableTask : Task, IMultiThreadableTask
    {
        /// <summary>
        /// Gets or sets the task environment that provides thread-safe alternatives to global process state APIs.
        /// This property is set by MSBuild before the task's Execute method is called.
        /// </summary>
        public TaskEnvironment TaskEnvironment { get; set; }
    }
}