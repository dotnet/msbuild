// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Registry that maintains an allowlist of tasks that are known to be thread-safe
    /// and can be executed within thread nodes in multi-threaded execution mode.
    /// </summary>
    internal static class ThreadSafeTaskRegistry
    {
        /// <summary>
        /// Set of task names that are known to be thread-safe and can run in multi-threaded mode.
        /// Initially contains core MSBuild tasks. This will be expanded over time as more tasks
        /// are verified to be thread-safe.
        /// </summary>
        private static readonly HashSet<string> s_threadSafeTasks = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Core MSBuild tasks that are known to be thread-safe
            "Message",
            "Warning", 
            "Error",
            "ItemGroup",
            "PropertyGroup",
            "CallTarget",
            "MSBuild"
        };

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

        /// <summary>
        /// Adds a task to the thread-safe allowlist. This is primarily for testing purposes
        /// and future extensibility when tasks implement the thread-safe interface.
        /// </summary>
        /// <param name="taskName">The name of the task to add to the allowlist.</param>
        internal static void AddThreadSafeTask(string taskName)
        {
            if (!string.IsNullOrEmpty(taskName))
            {
                s_threadSafeTasks.Add(taskName);
            }
        }

        /// <summary>
        /// Removes a task from the thread-safe allowlist. This is primarily for testing purposes.
        /// </summary>
        /// <param name="taskName">The name of the task to remove from the allowlist.</param>
        internal static void RemoveThreadSafeTask(string taskName)
        {
            if (!string.IsNullOrEmpty(taskName))
            {
                s_threadSafeTasks.Remove(taskName);
            }
        }

        /// <summary>
        /// Gets a copy of the current thread-safe task allowlist. This is primarily for testing purposes.
        /// </summary>
        /// <returns>A new HashSet containing the names of all thread-safe tasks.</returns>
        internal static HashSet<string> GetThreadSafeTasks()
        {
            return new HashSet<string>(s_threadSafeTasks, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Clears the thread-safe task allowlist. This is primarily for testing purposes.
        /// </summary>
        internal static void ClearThreadSafeTasks()
        {
            s_threadSafeTasks.Clear();
        }
    }
}