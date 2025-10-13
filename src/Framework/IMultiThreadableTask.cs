// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Interface for tasks that can execute in a thread-safe manner within MSBuild's multithreaded execution model. 
    /// Tasks that implement this interface declare their capability to run in multiple threads within one process.
    /// </summary>
    /// <remarks>
    /// The task must:
    /// 
    /// - Use the provided TaskEnvironment for all modifications to process state such as environment variables,
    ///   working directory, or process spawning instead of directly modifying global process state
    /// - Not depend on global process state for correct operation, including avoiding relative path 
    ///   resolution that relies on the current working directory
    /// - Use TaskEnvironment.GetAbsolutePath() instead of Path.GetFullPath() for path resolution
    /// - Handle any internal synchronization if the task spawns multiple threads internally
    /// 
    /// Tasks implementing this interface can be safely executed in parallel with other tasks or 
    /// instances of the same task within a single MSBuild process, enabling better performance
    /// in multithreaded build scenarios.
    /// 
    /// See the Thread-Safe Tasks specification for detailed guidelines on thread-safe task development.
    /// </remarks>
    public interface IMultiThreadableTask : ITask
    {
        /// <summary>
        /// This property is set by the build engine to allow a task to call back into it.
        /// </summary>
        /// <value>Task environment which provides access to project current directory and environment variables.</value>
        TaskEnvironment TaskEnvironment { get; internal set; }
    }
}