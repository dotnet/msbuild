// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Determines where a task should be executed in multi-threaded mode.
    /// In multi-threaded execution mode, tasks implementing IMultiThreadableTask or marked with
    /// MSBuildMultiThreadableTaskAttribute run in-process within thread nodes, while legacy tasks
    /// are routed to sidecar TaskHost processes for isolation.
    /// </summary>
    /// <remarks>
    /// This class should only be used when in multi-threaded mode. Traditional multi-proc builds
    /// have different semantics and should not use this routing logic.
    /// </remarks>
    internal static class TaskRouter
    {
        /// <summary>
        /// Cache of task types to their multi-threadable capability status.
        /// This avoids repeated reflection calls for the same task types.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, bool> s_multiThreadableTaskCache = new();

        /// <summary>
        /// Determines if a task needs to be routed to an out-of-process TaskHost sidecar
        /// in multi-threaded mode based on its thread-safety characteristics.
        /// </summary>
        /// <param name="taskType">The type of the task to evaluate.</param>
        /// <returns>
        /// True if the task should be executed in an out-of-process TaskHost sidecar;
        /// false if it can safely run in-process within a thread node.
        /// </returns>
        /// <remarks>
        /// This method only considers the task's thread-safety indicators.
        /// The caller is responsible for:
        /// - Only calling this in multi-threaded mode
        /// - Handling explicit out-of-proc requests (via TaskHostFactory or parameters)
        /// - Handling the isAlreadyOutOfProc scenario
        /// 
        /// In multi-threaded mode:
        /// - Tasks implementing IMultiThreadableTask OR marked with MSBuildMultiThreadableTaskAttribute
        ///   are considered thread-safe and can run in-process (returns false)
        /// - Tasks without these indicators must run in a sidecar TaskHost for isolation (returns true)
        /// </remarks>
        public static bool NeedsTaskHostInMultiThreadedMode(Type taskType)
        {
            ErrorUtilities.VerifyThrowArgumentNull(taskType, nameof(taskType));

            // Tasks without thread-safety indicators need isolation in a TaskHost sidecar
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
        /// By default, the interface has inheritable semantics - if a task inherits from a base class
        /// that implements IMultiThreadableTask, it is also considered thread-safe.
        /// However, if the base class is marked with InternalMSBuildTaskAttribute (tasks from MSBuild repo),
        /// non-inheritable semantics apply for backward compatibility.
        /// </summary>
        /// <param name="taskType">The task type to check.</param>
        /// <returns>True if the task should be considered multi-threadable; false otherwise.</returns>
        private static bool ImplementsIMultiThreadableTask(Type taskType)
        {
            // Check if task is assignable to IMultiThreadableTask
            if (!typeof(IMultiThreadableTask).IsAssignableFrom(taskType))
            {
                return false;
            }

            // Check if base type is also assignable to IMultiThreadableTask
            Type? baseType = taskType.BaseType;
            if (baseType != null && typeof(IMultiThreadableTask).IsAssignableFrom(baseType))
            {
                // Inherited from base type - non-inheritable if base is from MSBuild repo
                return false;
            }

            // Task implements the interface and base type doesn't
            // This means the task directly declared it
            return true;
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