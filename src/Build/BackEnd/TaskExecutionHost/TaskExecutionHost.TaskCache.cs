// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd;

internal partial class TaskExecutionHost
{
    private Dictionary<string, byte[]>? _taskCacheInputs;
    private TaskInvocationCache? _taskCache;
    private CancellationToken _taskCacheCancellationToken;

    private void CaptureTaskCacheInput(TaskPropertyInfo parameter, object value)
    {
        if (_taskCacheInputs is null)
        {
            return;
        }

        try
        {
            _taskCacheInputs[parameter.Name] = TaskInvocationCache.SerializeInputParameter(value);
        }
        catch (Exception e) when (TaskInvocationCache.IsCacheException(e))
        {
            if (e is not System.IO.InvalidDataException && ExceptionHandling.IsIoRelatedException(e))
            {
                ProjectErrorUtilities.ThrowInvalidProject(_taskLocation, "TaskCache.OperationFailed", _taskName, e.Message);
            }

            WarnTaskCacheUnavailable(e);
        }
    }

    internal bool TryRestoreTaskCache(ProjectTaskInstance task, TaskCacheDiagnosticCapture? diagnostics)
    {
        if (_taskCacheInputs is null || _cancelled)
        {
            return false;
        }

        if (diagnostics is null || diagnostics.HasBlockingDiagnostics || _taskLoggingContext.HasLoggedErrors)
        {
            _taskLoggingContext.LogComment(MessageImportance.Low, "TaskCache.Diagnostics", _taskName);
            return false;
        }

        try
        {
            string? rejection = null;
            if (_registeredTaskFactory is not null
                || _taskFactoryWrapper.TaskFactory is not AssemblyTaskFactory
                || TaskInstance.GetType() != _taskFactoryWrapper.TaskFactoryLoadedType.Type
                || TaskInstance.HostObject is not null)
            {
                rejection = ResourceUtilities.GetResourceString("TaskCache.DeclaredIOStructure");
            }
            else
            {
                _taskCache = TaskInvocationCache.TryCreate(
                    task, TaskInstance, _taskFactoryWrapper, _taskCacheInputs,
                    TaskEnvironment ?? Microsoft.Build.Framework.TaskEnvironment.Fallback, ProjectInstance,
                    _buildComponentHost.BuildParameters.BuildCacheDirectory, out rejection,
                    _buildComponentHost.BuildParameters.TaskCacheBackend, _taskCacheCancellationToken);
            }

            if (_taskCache is null)
            {
                _taskLoggingContext.LogWarning(null, new BuildEventFileInfo(_taskLocation),
                    "TaskCache.Rejected", _taskName, rejection);
                return false;
            }

            // Input getters and metadata access may themselves log diagnostics.
            if (diagnostics.HasBlockingDiagnostics || _taskLoggingContext.HasLoggedErrors)
            {
                _taskCache = null;
                return false;
            }

            bool hit = _taskCache.TryRestore();
            _taskLoggingContext.LogComment(hit ? MessageImportance.Normal : MessageImportance.Low,
                hit ? "TaskCache.Hit" : "TaskCache.Miss", _taskName);
            if (!hit)
            {
                diagnostics.BeginReplayablePhase();
            }
            return hit;
        }
        catch (TaskCacheRestoreException e)
        {
            // A partial restoration cannot safely fall back, even with ContinueOnError.
            ProjectErrorUtilities.ThrowInvalidProject(_taskLocation, "TaskCache.RestoreFailed", _taskName, e.Message);
            return false;
        }
        catch (Exception e) when (TaskInvocationCache.IsCacheException(e))
        {
            ProjectErrorUtilities.ThrowInvalidProject(_taskLocation, "TaskCache.OperationFailed", _taskName, e.Message);
            return false;
        }
    }

    internal void ReplayTaskCacheWarnings()
    {
        if (_taskCache?.IsHit == true)
        {
            foreach (TaskCacheWarning warning in _taskCache.Warnings)
            {
                _taskLoggingContext.LoggingService.LogBuildEvent(warning.ToEvent(_taskLoggingContext.BuildEventContext));
            }
        }
    }

    private object? GetTaskOutputValue(TaskPropertyInfo parameter)
    {
        if (_taskCache?.HasOutput(parameter.Name) == true)
        {
            // Return a fresh raw value for each binding. Binding an item output can mutate it.
            return _taskCache.GetOutput(parameter.Name);
        }

        object? value = _taskFactoryWrapper.GetPropertyValue(TaskInstance, parameter);
        if (_taskCache is not null)
        {
            _taskCache.CaptureOutput(parameter.Name, value);
            return _taskCache.GetOutput(parameter.Name);
        }

        return value;
    }

    private void CaptureTaskCacheOutputs(TaskCacheDiagnosticCapture? diagnostics)
    {
        if (_taskCache is null || _taskCache.IsHit || _cancelled || diagnostics is null)
        {
            return;
        }

        foreach (TaskPropertyInfo parameter in _taskCache.RequestedOutputs)
        {
            if (diagnostics.HasBlockingDiagnostics || _taskLoggingContext.HasLoggedErrors)
            {
                return;
            }
            if (_taskCache.HasOutput(parameter.Name))
            {
                continue;
            }

            try
            {
                GetTaskOutputValue(parameter);
            }
            catch (TargetInvocationException e)
            {
                ProjectErrorUtilities.ThrowInvalidProject(_taskLocation, "FailedToRetrieveTaskOutputs",
                    _taskName, parameter.Name, e.InnerException?.Message ?? e.Message);
            }
            catch (Exception e) when (!ExceptionHandling.NotExpectedReflectionException(e) || TaskInvocationCache.IsCacheException(e))
            {
                ProjectErrorUtilities.ThrowInvalidProject(_taskLocation, "FailedToRetrieveTaskOutputs",
                    _taskName, parameter.Name, e.Message);
            }
        }
    }

    internal void PublishTaskCache(TaskCacheDiagnosticCapture? diagnostics)
    {
        if (_taskCache is null || _taskCache.IsHit || _cancelled)
        {
            return;
        }

        if (diagnostics is null || diagnostics.HasBlockingDiagnostics || _taskLoggingContext.HasLoggedErrors)
        {
            _taskLoggingContext.LogComment(MessageImportance.Low, "TaskCache.Diagnostics", _taskName);
            return;
        }

        try
        {
            if (!_taskCache.Publish(() => !_cancelled && !diagnostics.HasBlockingDiagnostics && !_taskLoggingContext.HasLoggedErrors, diagnostics.GetWarnings())
                && !_cancelled && !diagnostics.HasBlockingDiagnostics && !_taskLoggingContext.HasLoggedErrors)
            {
                _taskLoggingContext.LogWarning(null, new BuildEventFileInfo(_taskLocation),
                    "TaskCache.InputsChanged", _taskName);
            }
        }
        catch (Exception e) when (TaskInvocationCache.IsCacheException(e))
        {
            ProjectErrorUtilities.ThrowInvalidProject(_taskLocation, "TaskCache.OperationFailed", _taskName, e.Message);
        }
    }

    private void WarnTaskCacheUnavailable(Exception exception)
    {
        _taskCache = null;
        _taskCacheInputs = null;
        _taskLoggingContext.LogWarning(null, new BuildEventFileInfo(_taskLocation),
            "TaskCache.Unavailable", _taskName, exception.Message);
    }
}
