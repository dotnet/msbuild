// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable enable

namespace Microsoft.Build.BackEnd.Components.Caching
{
    internal sealed class TaskResultCacheInvocation
    {
        private readonly IReadOnlyList<TaskResultCacheEvent>? _cachedEvents;
        private readonly TaskResultCacheEventCollector? _eventCollector;
        private readonly TaskLoggingContext _loggingContext;
        private readonly TaskResultCacheSession? _session;
        private readonly string _taskName;

        private TaskResultCacheInvocation(
            TaskLoggingContext loggingContext,
            string taskName,
            IReadOnlyList<TaskResultCacheEvent> cachedEvents)
        {
            _loggingContext = loggingContext;
            _taskName = taskName;
            _cachedEvents = cachedEvents;
        }

        private TaskResultCacheInvocation(
            TaskLoggingContext loggingContext,
            string taskName,
            TaskResultCacheSession session)
        {
            _loggingContext = loggingContext;
            _taskName = taskName;
            _session = session;
            _eventCollector = new TaskResultCacheEventCollector();
        }

        internal static async ValueTask<TaskResultCacheInvocation?> TryOpenAsync(
            TaskExecutionHost taskExecutionHost,
            ProjectTaskInstance taskNode,
            BuildRequestEntry buildRequestEntry,
            IBuildComponentHost componentHost,
            TaskLoggingContext loggingContext,
            CancellationToken cancellationToken)
        {
            ITask task = taskExecutionHost.TaskInstance;
            LoadedType? taskType = taskExecutionHost.TaskLoadedType;
            if (taskNode.Outputs.Count != 0 ||
                task is TaskHostTask ||
                taskType is null ||
                !taskType.HasMSBuildDeclaredIOTaskAttribute)
            {
                return null;
            }

            ProjectInstance project = buildRequestEntry.RequestConfiguration.Project;
            string? cacheDirectory = TaskResultCacheSession.ResolveCacheDirectory(
                project.GetPropertyValue(TaskResultCacheSession.CacheDirectoryPropertyName),
                project.GetPropertyValue(TaskResultCacheSession.CacheEnabledPropertyName));
            if (cacheDirectory is null)
            {
                return null;
            }

            ICollection<string> parameterNames = taskNode.ParametersForBuild.Keys;
            if (!IsEligible(taskType, parameterNames, out string? reason))
            {
                LogFailure(loggingContext, taskNode.Name, reason);
                return null;
            }

            var fileDigestCache = (TaskResultCacheFileDigestCache)componentHost.GetComponent(
                BuildComponentType.TaskResultCacheFileDigestCache);
            TaskResultCacheOpenResponse response = await TaskResultCacheSession.TryOpenAsync(
                task,
                parameterNames,
                project.FullPath,
                buildRequestEntry.ProjectRootDirectory,
                cacheDirectory,
                project.GetPropertyValue(TaskResultCacheSession.CacheSizeMBPropertyName),
                fileDigestCache,
                cancellationToken);

            switch (response.Result)
            {
                case TaskResultCacheOpenResult.Hit:
                    loggingContext.LogComment(
                        MessageImportance.Low,
                        "TaskResultCacheHit",
                        taskNode.Name,
                        response.Session!.Key);
                    return new TaskResultCacheInvocation(
                        loggingContext,
                        taskNode.Name,
                        response.Events!);

                case TaskResultCacheOpenResult.Miss:
                    LogFailure(loggingContext, taskNode.Name, response.Reason);
                    loggingContext.LogComment(
                        MessageImportance.Low,
                        "TaskResultCacheMiss",
                        taskNode.Name,
                        response.Session!.Key);
                    return new TaskResultCacheInvocation(
                        loggingContext,
                        taskNode.Name,
                        response.Session);

                default:
                    LogFailure(loggingContext, taskNode.Name, response.Reason);
                    return null;
            }
        }

        internal IDisposable? BeginEventCapture(TaskHost taskHost)
        {
            return _eventCollector is null
                ? null
                : taskHost.BeginTaskResultCacheEventCapture(_eventCollector);
        }

        internal bool TryReplay(TaskHost taskHost)
        {
            if (_cachedEvents is null)
            {
                return false;
            }

            for (int i = 0; i < _cachedEvents.Count; i++)
            {
                _cachedEvents[i].Replay(taskHost);
            }

            return true;
        }

        internal static async ValueTask TryStoreAsync(
            TaskResultCacheInvocation? invocation,
            bool taskReturned,
            bool taskResult,
            bool hasLoggedErrors,
            CancellationToken cancellationToken)
        {
            if (invocation?._session is null ||
                invocation._eventCollector is null ||
                !taskReturned ||
                !taskResult ||
                hasLoggedErrors)
            {
                return;
            }

            if (!invocation._eventCollector.IsSupported)
            {
                LogFailure(
                    invocation._loggingContext,
                    invocation._taskName,
                    "the task emitted an event type that the cache cannot replay");
                return;
            }

            TaskResultCacheStoreResponse response =
                await invocation._session.TryStoreAsync(
                    invocation._eventCollector.Events,
                    cancellationToken);
            if (!response.Success)
            {
                LogFailure(
                    invocation._loggingContext,
                    invocation._taskName,
                    response.Reason);
            }
        }

        private static bool IsEligible(
            LoadedType taskType,
            ICollection<string> parameterNames,
            out string? reason)
        {
            if (!taskType.HasValidMSBuildDeclaredIOAttributes)
            {
                reason = "the task has an invalid declared-I/O annotation";
                return false;
            }

            if (!ContainsParameter(parameterNames, "DeclaredInputs") ||
                !ContainsParameter(parameterNames, "DeclaredOutputs"))
            {
                reason = "the task invocation must explicitly supply DeclaredInputs and DeclaredOutputs";
                return false;
            }

            foreach (string requiredUnsetParameter in taskType.DeclaredIORequiredUnsetParameters)
            {
                if (ContainsParameter(parameterNames, requiredUnsetParameter))
                {
                    reason = $"the task parameter \"{requiredUnsetParameter}\" must be unset";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        private static bool ContainsParameter(
            ICollection<string> parameterNames,
            string expectedName)
        {
            foreach (string parameterName in parameterNames)
            {
                if (MSBuildNameIgnoreCaseComparer.Default.Equals(
                        parameterName,
                        expectedName))
                {
                    return true;
                }
            }

            return false;
        }

        private static void LogFailure(
            TaskLoggingContext loggingContext,
            string taskName,
            string? reason)
        {
            if (!String.IsNullOrEmpty(reason))
            {
                loggingContext.LogComment(
                    MessageImportance.Low,
                    "TaskResultCacheFailure",
                    taskName,
                    reason);
            }
        }
    }
}
