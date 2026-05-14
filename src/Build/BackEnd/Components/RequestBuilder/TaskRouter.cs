// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
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
        /// Cache of task types to their multi-threadable attribute status.
        /// This avoids repeated reflection calls for the same task types.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, bool> s_multiThreadableTaskCache = new();

        /// <summary>
        /// Well-known third-party tasks that have been verified safe to run in-process
        /// in multi-threaded mode, even though they don't carry the MSBuildMultiThreadableTaskAttribute.
        /// These tasks do not mutate shared static state or use process-wide resources unsafely.
        /// </summary>
        private static readonly FrozenSet<string> s_knownSafeTaskNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "NuGet.Build.Tasks.RestoreTask",
            "NuGet.Build.Tasks.RestoreTaskEx",
            "NuGet.Build.Tasks.GenerateRestoreGraphFileTask",
            "NuGet.Build.Tasks.WriteRestoreGraphTask",
            "NuGet.Build.Tasks.GetRestoreProjectReferencesTask",
            "NuGet.Build.Tasks.GetRestorePackageReferencesTask",
            "NuGet.Build.Tasks.GetCentralPackageVersionsTask",
            "NuGet.Build.Tasks.GetRestorePackageDownloadsTask",
            "NuGet.Build.Tasks.GetRestoreFrameworkReferencesTask",
            "NuGet.Build.Tasks.GetRestoreNuGetAuditSuppressionsTask",
            "NuGet.Build.Tasks.GetRestorePrunePackageReferencesTask",
            "NuGet.Build.Tasks.GetRestoreDotnetCliToolsTask",
            "NuGet.Build.Tasks.GetProjectTargetFrameworksTask",
            "NuGet.Build.Tasks.GetRestoreSolutionProjectsTask",
            "NuGet.Build.Tasks.GetRestoreSettingsTask",
            "NuGet.Build.Tasks.WarnForInvalidProjectsTask",
            "NuGet.Build.Tasks.GetReferenceNearestTargetFrameworkTask",
            "NuGet.Build.Tasks.GetRestoreProjectStyleTask",
            "NuGet.Build.Tasks.NuGetMessageTask",
            "NuGet.Build.Tasks.CheckForDuplicateNuGetItemsTask",
            "NuGet.Build.Tasks.GetGlobalPropertyValueTask",
            "NuGet.Build.Tasks.Pack.PackTask",
            "NuGet.Build.Tasks.Pack.GetPackOutputItemsTask",
            "NuGet.Build.Tasks.Pack.GetProjectReferencesFromAssetsFileTask",
            "NuGet.Build.Tasks.Pack.IsPackableFalseWarningTask",
        }.ToFrozenSet();

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
        /// - Tasks marked with MSBuildMultiThreadableTaskAttribute (non-inheritable) are considered
        ///   thread-safe and can run in-process (returns false)
        /// - Tasks in the known-safe list are considered thread-safe (returns false)
        /// - Tasks without the attribute must run in a sidecar TaskHost for isolation (returns true)
        /// </remarks>
        public static bool NeedsTaskHostInMultiThreadedMode(Type taskType)
        {
            ErrorUtilities.VerifyThrowArgumentNull(taskType, nameof(taskType));

            // Tasks without the thread-safety attribute or not in the known-safe list need isolation in a TaskHost sidecar
            return !IsKnownSafeTask(taskType) && !HasMultiThreadableTaskAttribute(taskType);
        }

        /// <summary>
        /// Checks if a task type is in the well-known list of third-party tasks that are safe
        /// to run in-process in multi-threaded mode.
        /// </summary>
        /// <param name="taskType">The task type to check.</param>
        /// <returns>True if the task is known to be safe; false otherwise.</returns>
        private static bool IsKnownSafeTask(Type taskType)
        {
            return taskType.FullName is not null && s_knownSafeTaskNames.Contains(taskType.FullName);
        }

        /// <summary>
        /// Checks if a task type is marked with MSBuildMultiThreadableTaskAttribute.
        /// Detection is based on namespace and name only, ignoring the defining assembly,
        /// which allows customers to define the attribute in their own assemblies.
        /// Results are cached to avoid repeated reflection calls.
        /// </summary>
        /// <param name="taskType">The task type to check.</param>
        /// <returns>True if the task has the attribute; false otherwise.</returns>
        private static bool HasMultiThreadableTaskAttribute(Type taskType)
        {
            return s_multiThreadableTaskCache.GetOrAdd(
                taskType,
                static t =>
                {
                    const string attributeFullName = "Microsoft.Build.Framework.MSBuildMultiThreadableTaskAttribute";

                    // Check for the attribute by full name, not by type identity
                    // This allows custom-defined attributes from different assemblies
                    foreach (object attr in t.GetCustomAttributes(inherit: false))
                    {
                        if (attr.GetType().FullName == attributeFullName)
                        {
                            return true;
                        }
                    }

                    return false;
                });
        }

        /// <summary>
        /// Clears the thread-safety cache. Used primarily for testing.
        /// </summary>
        internal static void ClearCache()
        {
            s_multiThreadableTaskCache.Clear();
        }
    }
}
