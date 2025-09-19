// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Temporary registry that maintains an allowlist of tasks that are known to be thread-safe
    /// and can be executed within thread nodes in multi-threaded execution mode.
    /// 
    /// This is an interim solution until we adopt a proper thread-safe task interface.
    /// Tasks listed here have been manually verified to be thread-safe in their current
    /// implementation. Once a formal thread-safe task interface is established, this
    /// allowlist approach will be replaced with interface-based detection.
    /// </summary>
    internal static class ThreadSafeTaskRegistry
    {
        /// <summary>
        /// Set of task names that are known to be thread-safe and can run in multi-threaded mode.
        /// Initially contains core MSBuild tasks. This will be expanded over time as more tasks
        /// are verified to be thread-safe.
        /// </summary>
        private static readonly FrozenSet<string> s_threadSafeTasks = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Core MSBuild tasks that are known to be thread-safe
            "Message",
            "Warning", 
            "Error",
            "ItemGroup",
            "PropertyGroup",
            "CallTarget",
            "MSBuild"
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Determines if a task is known to be thread-safe and can be executed within a thread node.
        /// </summary>
        /// <param name="taskName">The name of the task to check.</param>
        /// <returns>True if the task is on the thread-safe allowlist, false otherwise.</returns>
        public static bool IsTaskThreadSafe(string taskName)
        {
            if (string.IsNullOrEmpty(taskName))
            {
                return false;
            }

            return s_threadSafeTasks.Contains(taskName);
        }
    }
}