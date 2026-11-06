// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd;

#nullable disable

namespace Microsoft.Build.Shared;

/// <summary>
///  Represents the result of executing a task in an out-of-process task host.
/// </summary>
internal class OutOfProcTaskHostTaskResult
{
    /// <summary>
    ///  Initializes a new instance of the <see cref="OutOfProcTaskHostTaskResult"/> class with an execution result.
    /// </summary>
    /// <param name="result">The overall result of task execution.</param>
    internal OutOfProcTaskHostTaskResult(TaskCompleteType result)
        : this(result, finalParams: null, taskException: null, exceptionMessage: null, exceptionMessageArgs: null)
    {
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="OutOfProcTaskHostTaskResult"/> class with final task parameter values.
    /// </summary>
    /// <param name="result">The overall result of task execution.</param>
    /// <param name="finalParams">The final task parameter values, or <see langword="null"/> if none are available.</param>
    internal OutOfProcTaskHostTaskResult(TaskCompleteType result, IDictionary<string, object> finalParams)
        : this(result, finalParams, taskException: null, exceptionMessage: null, exceptionMessageArgs: null)
    {
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="OutOfProcTaskHostTaskResult"/> class with a task exception.
    /// </summary>
    /// <param name="result">The overall result of task execution.</param>
    /// <param name="taskException">The task host exception, or <see langword="null"/> if no exception occurred.</param>
    internal OutOfProcTaskHostTaskResult(TaskCompleteType result, Exception taskException)
        : this(result, finalParams: null, taskException, exceptionMessage: null, exceptionMessageArgs: null)
    {
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="OutOfProcTaskHostTaskResult"/> class with a task exception and an accompanying message.
    /// </summary>
    /// <param name="result">The overall result of task execution.</param>
    /// <param name="taskException">The task host exception, or <see langword="null"/> if no exception occurred.</param>
    /// <param name="exceptionMessage">The resource name of an accompanying error message, or <see langword="null"/> if none is provided.</param>
    /// <param name="exceptionMessageArgs">The message formatting arguments, or <see langword="null"/> if none are provided.</param>
    internal OutOfProcTaskHostTaskResult(TaskCompleteType result, Exception taskException, string exceptionMessage, string[] exceptionMessageArgs)
        : this(result, finalParams: null, taskException, exceptionMessage, exceptionMessageArgs)
    {
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="OutOfProcTaskHostTaskResult"/> class with final task parameter values and exception details.
    /// </summary>
    /// <param name="result">The overall result of task execution.</param>
    /// <param name="finalParams">The final task parameter values, or <see langword="null"/> if none are available.</param>
    /// <param name="taskException">The task host exception, or <see langword="null"/> if no exception occurred.</param>
    /// <param name="exceptionMessage">The resource name of an accompanying error message, or <see langword="null"/> if none is provided.</param>
    /// <param name="exceptionMessageArgs">The message formatting arguments, or <see langword="null"/> if none are provided.</param>
    /// <remarks>
    ///  A crash result requires <paramref name="taskException"/>. An accompanying message requires a crash result,
    ///  and nonempty <paramref name="exceptionMessageArgs"/> require <paramref name="exceptionMessage"/>.
    /// </remarks>
    internal OutOfProcTaskHostTaskResult(
        TaskCompleteType result,
        IDictionary<string, object> finalParams,
        Exception taskException,
        string exceptionMessage,
        string[] exceptionMessageArgs)
    {
        // If we're returning a crashing result, we should always also be returning the exception that caused the crash, although
        // we may not always be returning an accompanying message.
        if (result is TaskCompleteType.CrashedDuringInitialization or
                      TaskCompleteType.CrashedDuringExecution or
                      TaskCompleteType.CrashedAfterExecution)
        {
            Assumed.NotNull(taskException);
        }

        if (exceptionMessage != null)
        {
            Assumed.True(
                result is TaskCompleteType.CrashedDuringInitialization or
                          TaskCompleteType.CrashedDuringExecution or
                          TaskCompleteType.CrashedAfterExecution,
                "If we have an exception message, the result type should be 'crashed' of some variety.");
        }

        if (exceptionMessageArgs?.Length > 0)
        {
            Assumed.NotNull(exceptionMessage, "If we have message args, we need a message.");
        }

        Result = result;
        FinalParameterValues = finalParams;
        TaskException = taskException;
        ExceptionMessage = exceptionMessage;
        ExceptionMessageArgs = exceptionMessageArgs;
    }

    /// <summary>
    ///  Gets the overall result of task execution.
    /// </summary>
    public TaskCompleteType Result { get; }

    /// <summary>
    ///  Gets the final task parameter values, or <see langword="null"/> if none are available.
    /// </summary>
    public IDictionary<string, object> FinalParameterValues { get; }

    /// <summary>
    ///  Gets the exception that occurred during task host initialization, execution, or post-execution processing,
    ///  or <see langword="null"/> if no exception occurred.
    /// </summary>
    public Exception TaskException { get; }

    /// <summary>
    ///  Gets the resource name of the message to log with <see cref="TaskException"/>,
    ///  or <see langword="null"/> if no accompanying message is provided.
    /// </summary>
    public string ExceptionMessage { get; }

    /// <summary>
    ///  Gets the arguments used to format <see cref="ExceptionMessage"/>,
    ///  or <see langword="null"/> if no arguments are provided.
    /// </summary>
    public string[] ExceptionMessageArgs { get; }
}
