// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd;

namespace Microsoft.Build.Shared;

/// <summary>
/// A result of executing a target or task.
/// </summary>
internal class OutOfProcTaskHostTaskResult
{
    /// <summary>
    /// The overall result of the task execution.
    /// </summary>
    public TaskCompleteType Result { get; }

    /// <summary>
    /// Dictionary of the final values of the task parameters.
    /// </summary>
    public Dictionary<string, object?>? FinalParameterValues { get; }

    /// <summary>
    /// The exception thrown by the task during initialization or execution, if any.
    /// </summary>
    public Exception? TaskException { get; }

    /// <summary>
    /// The name of the resource representing the message to be logged along with the
    /// above exception.
    /// </summary>
    public string? ExceptionMessage { get; }

    /// <summary>
    /// The arguments to be used when formatting ExceptionMessage.
    /// </summary>
    public string[]? ExceptionMessageArgs { get; }

    private OutOfProcTaskHostTaskResult(
        TaskCompleteType result,
        Dictionary<string, object?>? finalParameterValues)
    {
        Result = result;
        FinalParameterValues = finalParameterValues;
    }

    private OutOfProcTaskHostTaskResult(
        TaskCompleteType result,
        Exception? taskException = null,
        string? exceptionMessage = null,
        string[]? exceptionMessageArgs = null)
    {
        Result = result;
        TaskException = taskException;
        ExceptionMessage = exceptionMessage;
        ExceptionMessageArgs = exceptionMessageArgs;
    }

    public static OutOfProcTaskHostTaskResult Success(Dictionary<string, object?>? finalParameterValues)
        => new(TaskCompleteType.Success, finalParameterValues);

    public static OutOfProcTaskHostTaskResult Failure(Dictionary<string, object?>? finalParameterValues = null)
        => new(TaskCompleteType.Failure, finalParameterValues);

    public static OutOfProcTaskHostTaskResult CrashedAfterExecution(Exception e)
        => new(TaskCompleteType.CrashedAfterExecution, e);

    public static OutOfProcTaskHostTaskResult CrashedDuringExecution(Exception e)
        => new(TaskCompleteType.CrashedDuringExecution, e);

    public static OutOfProcTaskHostTaskResult CrashedDuringInitialization(Exception e, string resourceId, string[] resourceArgs)
        => new(TaskCompleteType.CrashedDuringInitialization, e, resourceId, resourceArgs);
}
