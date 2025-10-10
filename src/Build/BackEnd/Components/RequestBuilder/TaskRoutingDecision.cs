// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Determines where a task should be executed based on its characteristics.
    /// In multi-threaded execution mode, tasks implementing IMultiThreadableTask or marked with
    /// MSBuildMultiThreadableTaskAttribute run in-process within thread nodes, while legacy tasks
    /// are routed to sidecar TaskHost processes.
    /// </summary>
    internal static class TaskRoutingDecision
    {
        /// <summary>
        /// Cache of task types to their multi-threadable capability status.
        /// This avoids repeated reflection calls for the same task types.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, bool> s_multiThreadableTaskCache = new();

        /// <summary>
        /// Determines if a task should be executed out-of-process based on its characteristics
        /// and the current execution mode.
        /// </summary>
        /// <param name="taskType">The type of the task to evaluate.</param>
        /// <param name="isAlreadyOutOfProc">Whether the build is already running in an out-of-process node.</param>
        /// <param name="multiThreadedMode">Whether multi-threaded execution mode is enabled.</param>
        /// <param name="isOutOfProcExplicitlyRequested">Whether out-of-process execution was explicitly requested
        /// (e.g., via TaskHostFactory or task parameters).</param>
        /// <returns>
        /// True if the task should be executed in an out-of-process TaskHost; false if it should
        /// run in-process within a thread node.
        /// </returns>
        /// <remarks>
        /// The routing decision follows this priority:
        /// 1. If already out-of-process → stay out-of-process
        /// 2. If explicitly requested out-of-process → go out-of-process
        /// 3. If not in multi-threaded mode → use legacy in-process behavior
        /// 4. If in multi-threaded mode:
        ///    - Tasks implementing IMultiThreadableTask OR marked with MSBuildMultiThreadableTaskAttribute → run in-process (thread node)
        ///    - Tasks NOT implementing interface or attribute → run out-of-process (sidecar TaskHost)
        /// </remarks>
        public static bool ShouldExecuteOutOfProc(
            Type taskType,
            bool isAlreadyOutOfProc,
            bool multiThreadedMode,
            bool isOutOfProcExplicitlyRequested)
        {
            ErrorUtilities.VerifyThrowArgumentNull(taskType, nameof(taskType));

            // Already out-of-process? Stay there.
            if (isAlreadyOutOfProc)
            {
                return true;
            }

            // Explicitly requested out-of-process? Honor it regardless of thread-safety indicators.
            if (isOutOfProcExplicitlyRequested)
            {
                return true;
            }

            // Not in multi-threaded mode? Use legacy behavior (in-process).
            if (!multiThreadedMode)
            {
                return false;
            }

            // Multi-threaded mode: route based on thread-safety indicators (interface or attribute).
            // Tasks with thread-safety indicators can safely run in-process in a thread node.
            // Tasks without them must run in a sidecar TaskHost for isolation.
            return !IsMultiThreadableTask(taskType);
        }

        /// <summary>
        /// Checks if a task is multi-threadable via IMultiThreadableTask interface OR
        /// MSBuildMultiThreadableTaskAttribute attribute.
        /// Results are cached to avoid repeated reflection calls.
        /// </summary>
        /// <param name="taskType">The task type to check.</param>
        /// <returns>True if the task has thread-safety indicators; false otherwise.</returns>
        private static bool IsMultiThreadableTask(Type taskType)
        {
            return s_multiThreadableTaskCache.GetOrAdd(
                taskType,
                static t =>
                    ImplementsIMultiThreadableTask(t) ||
                    HasMultiThreadableTaskAttribute(t));
        }

        /// <summary>
        /// Checks if a task type implements the IMultiThreadableTask interface.
        /// </summary>
        /// <param name="taskType">The task type to check.</param>
        /// <returns>True if the task implements IMultiThreadableTask; false otherwise.</returns>
        private static bool ImplementsIMultiThreadableTask(Type taskType)
        {
            return typeof(IMultiThreadableTask).IsAssignableFrom(taskType);
        }

        /// <summary>
        /// Checks if a task type is marked with MSBuildMultiThreadableTaskAttribute.
        /// Detection is based on namespace and name only, ignoring the defining assembly,
        /// which allows customers to define the attribute in their own assemblies.
        /// </summary>
        /// <param name="taskType">The task type to check.</param>
        /// <returns>True if the task has the attribute; false otherwise.</returns>
        private static bool HasMultiThreadableTaskAttribute(Type taskType)
        {
            const string attributeFullName = "Microsoft.Build.Framework.MSBuildMultiThreadableTaskAttribute";

            // Check for the attribute by full name, not by type identity
            // This allows custom-defined attributes from different assemblies
            foreach (object attr in taskType.GetCustomAttributes(inherit: false))
            {
                if (attr.GetType().FullName == attributeFullName)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Clears the thread-safety capability cache. Used primarily for testing.
        /// </summary>
        internal static void ClearCache()
        {
            s_multiThreadableTaskCache.Clear();
        }
    }
}
