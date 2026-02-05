// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.TaskHost.Utilities;

namespace Microsoft.Build.TaskHost.BackEnd;

/// <summary>
/// How the task completed -- successful, failed, or crashed.
/// </summary>
internal enum TaskCompleteType
{
    /// <summary>
    /// Task execution succeeded
    /// </summary>
    Success,

    /// <summary>
    /// Task execution failed
    /// </summary>
    Failure,

    /// <summary>
    /// Task crashed during initialization steps -- loading the task,
    /// validating or setting the parameters, etc.
    /// </summary>
    CrashedDuringInitialization,

    /// <summary>
    /// Task crashed while being executed
    /// </summary>
    CrashedDuringExecution,

    /// <summary>
    /// Task crashed after being executed
    /// -- Getting outputs, etc
    /// </summary>
    CrashedAfterExecution
}

/// <summary>
/// TaskHostTaskComplete contains all the information the parent node
/// needs from the task host on completion of task execution.
/// </summary>
internal sealed class TaskHostTaskComplete : INodePacket
{
    /// <summary>
    /// Result of the task's execution.
    /// </summary>
    private TaskCompleteType _taskResult;

    /// <summary>
    /// If the task threw an exception during its initialization or execution,
    /// save it here.
    /// </summary>
    private Exception? _taskException;

    /// <summary>
    /// If there's an additional message that should be attached to the error
    /// logged beyond "task X failed unexpectedly", save it here.  May be null.
    /// </summary>
    private string? _taskExceptionMessage;

    /// <summary>
    /// If the message saved in taskExceptionMessage requires arguments, save
    /// them here. May be null.
    /// </summary>
    private string[]? _taskExceptionMessageArgs;

    /// <summary>
    /// The set of parameters / values from the task after it finishes execution.
    /// </summary>
    private Dictionary<string, TaskParameter>? _taskOutputParameters;

    /// <summary>
    /// The process environment at the end of task execution.
    /// </summary>
    private Dictionary<string, string?>? _buildProcessEnvironment;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskHostTaskComplete"/> class.
    /// </summary>
    /// <param name="result">The result of the task's execution.</param>
    /// <param name="buildProcessEnvironment">The build process environment as it was at the end of the task's execution.</param>
    public TaskHostTaskComplete(
        OutOfProcTaskHostTaskResult result,
        Dictionary<string, string?>? buildProcessEnvironment)
    {
        ErrorUtilities.VerifyThrowInternalNull(result);

        _taskResult = result.Result;
        _taskException = result.TaskException;
        _taskExceptionMessage = result.ExceptionMessage;
        _taskExceptionMessageArgs = result.ExceptionMessageArgs;

        if (result.FinalParameterValues != null)
        {
            _taskOutputParameters = new(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, object?> parameter in result.FinalParameterValues)
            {
                _taskOutputParameters[parameter.Key] = new TaskParameter(parameter.Value);
            }
        }

        _buildProcessEnvironment = buildProcessEnvironment;
    }

    private TaskHostTaskComplete()
    {
    }

    /// <summary>
    /// Gets the result of the task's execution.
    /// </summary>
    public TaskCompleteType TaskResult => _taskResult;

    /// <summary>
    /// Gets the exception thrown be the task during initialization or execution, if any.
    /// save it here.
    /// </summary>
    public Exception? TaskException => _taskException;

    /// <summary>
    /// If there's an additional message that should be attached to the error
    /// logged beyond "task X failed unexpectedly", put it here.  May be null.
    /// </summary>
    public string? TaskExceptionMessage => _taskExceptionMessage;

    /// <summary>
    /// If there are arguments that need to be formatted into the message being
    /// sent, set them here.  May be null.
    /// </summary>
    public string[]? TaskExceptionMessageArgs => _taskExceptionMessageArgs;

    /// <summary>
    /// Task parameters and their values after the task has finished.
    /// </summary>
    public Dictionary<string, TaskParameter>? TaskOutputParameters => _taskOutputParameters ??= new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The process environment.
    /// </summary>
    public Dictionary<string, string?>? BuildProcessEnvironment => _buildProcessEnvironment;

    /// <summary>
    /// Gets the type of this packet.
    /// </summary>
    public NodePacketType Type => NodePacketType.TaskHostTaskComplete;

    /// <summary>
    /// Translates the packet to/from binary form.
    /// </summary>
    /// <param name="translator">The translator to use.</param>
    public void Translate(ITranslator translator)
    {
        translator.TranslateEnum(ref _taskResult, (int)_taskResult);
        translator.TranslateException(ref _taskException);
        translator.Translate(ref _taskExceptionMessage);
        translator.Translate(ref _taskExceptionMessageArgs);
        translator.TranslateDictionary(ref _taskOutputParameters, StringComparer.OrdinalIgnoreCase, TaskParameter.FactoryForDeserialization);
        translator.TranslateDictionary(ref _buildProcessEnvironment, StringComparer.OrdinalIgnoreCase);
        bool hasFileAccessData = false;
        translator.Translate(ref hasFileAccessData);
    }

    internal static INodePacket FactoryForDeserialization(ITranslator translator)
    {
        var taskComplete = new TaskHostTaskComplete();
        taskComplete.Translate(translator);
        return taskComplete;
    }
}
