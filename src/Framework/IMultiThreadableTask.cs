// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Interface for tasks that can execute in a thread-safe manner.
    /// Tasks that implement this interface guarantee they handle their own thread safety
    /// and can be safely executed in parallel with other tasks or instances of the same task.
    /// </summary>
    public interface IMultiThreadableTask : ITask
    {
        /// <summary>
        /// Gets or sets the task environment that provides access to task execution environment.
        /// This property must be set by the MSBuild infrastructure before task execution.
        /// </summary>
        TaskEnvironment TaskEnvironment { get; set; }
    }
}
