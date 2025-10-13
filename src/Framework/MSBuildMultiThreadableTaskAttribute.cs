// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Attribute that marks a task class as thread-safe for multithreaded execution.
    /// </summary>
    /// <remarks>
    /// Task classes marked with this attribute indicate they can be safely executed in parallel 
    /// in the same process with other tasks. This is a compatibility bridge option for existing tasks
    /// that do not have access to TaskEnvironment APIs.
    /// 
    /// Tasks using this attribute must satisfy strict requirements:
    /// - Must not modify global process state (environment variables, working directory, etc.)
    /// - Must not depend on global process state, including relative path resolution
    /// 
    /// MSBuild detects this attribute by its namespace and name only, ignoring the defining assembly.
    /// This allows customers to define the attribute in their own assemblies alongside their tasks.
    /// Since MSBuild does not ship this attribute, task authors can copy this definition into their
    /// own projects and mark their task classes with it. Customers using newer MSBuild versions
    /// should prefer implementing IMultiThreadableTask which provides access to TaskEnvironment
    /// for safe process state operations.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    internal class MSBuildMultiThreadableTaskAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the ThreadSafeAttribute class.
        /// </summary>
        public MSBuildMultiThreadableTaskAttribute()
        {
        }
    }
}