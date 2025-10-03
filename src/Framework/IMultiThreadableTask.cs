// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This interface defines a multithreadable task in the build system. Tasks that implement this interface
    /// indicate they can be safely executed in parallel with other tasks in a multithreaded build environment.
    /// Thread-safe tasks must not depend on process-global state such as current working directory,
    /// environment variables, or process-wide culture settings.
    /// 
    /// Tasks implementing this interface receive a TaskEnvironment object that provides thread-safe
    /// alternatives to global process state APIs.
    /// </summary>
    public interface IMultiThreadableTask : ITask
    {
        /// <summary>
        /// Gets or sets the task environment that provides thread-safe alternatives to global process state APIs.
        /// This property is set by MSBuild before the task's Execute method is called.
        /// </summary>
        TaskEnvironment TaskEnvironment { get; set; }
    }
}