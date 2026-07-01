// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Describes an explicit override of the engine's multi-threaded task-routing decision.
    /// </summary>
    /// <remarks>
    /// The override may come from an environment variable (prototype / testing surface, see
    /// <see cref="TaskRouter.GetEnvironmentRoutingOverride"/>) or, in a separate design, from
    /// <c>UsingTask</c> metadata. It only has meaning in multi-threaded mode.
    /// </remarks>
    internal enum TaskHostRoutingOverride
    {
        /// <summary>
        /// No override; the engine applies its default routing decision.
        /// </summary>
        None,

        /// <summary>
        /// Force the task to run in-process within the thread node.
        /// </summary>
        InProc,

        /// <summary>
        /// Force the task to run in a reusable (sidecar) TaskHost process.
        /// </summary>
        Sidecar,

        /// <summary>
        /// Force the task to run in a transient TaskHost process that terminates after execution.
        /// </summary>
        Transient,
    }

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
    /// - Tasks without the attribute must run in a sidecar TaskHost for isolation (returns true)
    /// </remarks>
    public static bool NeedsTaskHostInMultiThreadedMode(Type taskType)
    {
        ArgumentNullException.ThrowIfNull(taskType);

        // Tasks without the thread-safety attribute need isolation in a TaskHost sidecar
        return !HasMultiThreadableTaskAttribute(taskType);
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
        /// Full name of a task whose static singleton state makes it unsafe to run in a
        /// long-lived sidecar TaskHost (which persists across invocations). Such tasks must
        /// instead run in an explicit (transient) TaskHost that terminates after execution,
        /// ensuring static state is cleaned up.
        /// This is a temporary workaround until the task authors fix their static state issues.
        /// See https://github.com/dotnet/msbuild/issues/13315
        /// </summary>
        private const string TaskRequiringTransientTaskHostFullName = "NuGet.Build.Tasks.RestoreTask";

        /// <summary>
        /// Determines if a task must be routed to an explicit (transient) TaskHost rather than
        /// a reusable sidecar, because its static singleton state would leak across invocations.
        /// Such tasks should run in a TaskHost that terminates after execution so all static
        /// state is cleaned up.
        /// </summary>
        /// <param name="taskType">The type of the task to evaluate.</param>
        /// <returns>True if the task requires a transient TaskHost; false otherwise.</returns>
        public static bool RequiresTransientTaskHost(Type taskType)
        {
            ArgumentNullException.ThrowIfNull(taskType);

            string? fullName = taskType.FullName;
            if (fullName is null)
            {
                return false;
            }

            return string.Equals(fullName, TaskRequiringTransientTaskHostFullName, StringComparison.Ordinal);
        }

        /// <summary>
        /// PROTOTYPE / TESTING ONLY. Determines whether an environment variable explicitly overrides
        /// the engine's routing decision for the given task, forcing it in-process, into a transient
        /// TaskHost, or into a sidecar TaskHost.
        /// </summary>
        /// <param name="taskType">The type of the task to evaluate.</param>
        /// <returns>
        /// The requested <see cref="TaskHostRoutingOverride"/>, or <see cref="TaskHostRoutingOverride.None"/>
        /// when the task is not named in any of the override environment variables.
        /// </returns>
        /// <remarks>
        /// The override is controlled by the following environment variables, each a comma- or
        /// semicolon-delimited list of task full names (Namespace.TaskName):
        /// <list type="bullet">
        /// <item><c>MSBUILDFORCETASKSINPROCHOSTLIST</c> -&gt; <see cref="TaskHostRoutingOverride.InProc"/></item>
        /// <item><c>MSBUILDFORCETASKSTRANSIENTHOSTLIST</c> -&gt; <see cref="TaskHostRoutingOverride.Transient"/></item>
        /// <item><c>MSBUILDFORCETASKSSIDECARHOSTLIST</c> -&gt; <see cref="TaskHostRoutingOverride.Sidecar"/></item>
        /// </list>
        /// If a task appears in more than one list, in-proc wins over transient, which wins over sidecar.
        /// This surface exists to unblock testing of multi-threaded execution against tasks the author
        /// cannot change; it is not a supported production configuration.
        /// </remarks>
        public static TaskHostRoutingOverride GetEnvironmentRoutingOverride(Type taskType)
        {
            ArgumentNullException.ThrowIfNull(taskType);

            string? fullName = taskType.FullName;
            if (fullName is null)
            {
                return TaskHostRoutingOverride.None;
            }

            Traits traits = Traits.Instance;

            if (traits.ForceTasksInProcHostList.Contains(fullName))
            {
                return TaskHostRoutingOverride.InProc;
            }

            if (traits.ForceTasksTransientHostList.Contains(fullName))
            {
                return TaskHostRoutingOverride.Transient;
            }

            if (traits.ForceTasksSidecarHostList.Contains(fullName))
            {
                return TaskHostRoutingOverride.Sidecar;
            }

            return TaskHostRoutingOverride.None;
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
