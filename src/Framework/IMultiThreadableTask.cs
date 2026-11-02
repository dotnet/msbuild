// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Interface for tasks that can execute in a thread-safe manner within MSBuild's multithreaded execution model. 
    /// Tasks that implement this interface declare their capability to run in multiple threads within one process.
    /// </summary>
    /// <remarks>
    /// The task <strong>must</strong>:
    /// <list type="bullet">
    /// <item>Use the provided <see cref="TaskEnvironment"/> for all modifications to process state such as environment variables,
    ///   working directory, or process spawning instead of directly modifying global process state</item>
    /// <item>Not depend on global process state for correct operation, including avoiding relative path
    ///   resolution that relies on the current working directory</item>
    /// <item>Use <see cref="TaskEnvironment.GetAbsolutePath(string)"/> instead of <see cref="System.IO.Path.GetFullPath(string)"/> for path resolution</item>
    /// <item>Handle any internal synchronization if the task spawns multiple threads internally</item>
    /// </list>
    /// Tasks implementing this interface can be safely executed in parallel with other tasks or
    /// instances of the same task within a single MSBuild process, enabling better performance
    /// in multithreaded build scenarios.
    /// 
    /// See the <see href="https://github.com/dotnet/msbuild/blob/main/documentation/specs/multithreading/thread-safe-tasks.md">Thread-Safe Tasks specification</see> for detailed guidelines on thread-safe task development.
    /// </remarks>
    public interface IMultiThreadableTask : ITask
    {
        /// <summary>
        /// Gets or sets the task execution environment, which provides access to project current directory and environment variables in a thread-safe manner.
        /// </summary>
        /// <remarks>
        /// The MSBuild engine sets this property on behalf of the task before the task is executed, so a task should
        /// not overwrite it during execution. The one exception is constructor injection: if a task declares a public
        /// constructor that takes a <see cref="TaskEnvironment"/>, the engine invokes that constructor with the current
        /// environment, and the task is expected to assign this property from within it. This lets the task compute
        /// environment-dependent default values during construction (property initializers run before the engine could
        /// otherwise assign the property).
        /// </remarks>
        /// <value>Task environment which provides access to project current directory and environment variables.</value>
        TaskEnvironment TaskEnvironment { get; set; }
    }
}
