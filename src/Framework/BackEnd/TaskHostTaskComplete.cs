// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
#if FEATURE_REPORTFILEACCESSES
using Microsoft.Build.Experimental.FileAccess;
#endif
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.BackEnd;

/// <summary>
/// TaskHostTaskComplete contains all the information the owning worker node
/// needs from the task host on completion of task execution.
/// </summary>
internal class TaskHostTaskComplete : INodePacket
{
#if FEATURE_REPORTFILEACCESSES
    private List<FileAccessData> _fileAccessData;
#endif

    /// <summary>
    /// Result of the task's execution.
    /// </summary>
    private TaskCompleteType _taskResult;

    /// <summary>
    /// If the task threw an exception during its initialization or execution,
    /// save it here.
    /// </summary>
    private Exception _taskException;

    /// <summary>
    /// If there's an additional message that should be attached to the error
    /// logged beyond "task X failed unexpectedly", save it here.  May be null.
    /// </summary>
    private string _taskExceptionMessage;

    /// <summary>
    /// If the message saved in taskExceptionMessage requires arguments, save
    /// them here. May be null.
    /// </summary>
    private string[] _taskExceptionMessageArgs;

    /// <summary>
    /// The set of parameters / values from the task after it finishes execution.
    /// </summary>
    private Dictionary<string, TaskParameter> _taskOutputParameters = null;

    /// <summary>
    /// The process environment at the end of task execution.
    /// </summary>
    private Dictionary<string, string> _buildProcessEnvironment = null;

    /// <summary>
    /// How <see cref="_buildProcessEnvironment"/> is represented on the wire. Defaults to
    /// <see cref="InvariantPayloadTransferMode.Full"/> so older (legacy) code paths keep their existing behavior.
    /// </summary>
    private InvariantPayloadTransferMode _environmentMode = InvariantPayloadTransferMode.Full;

    /// <summary>
    ///  Initializes a new instance of the <see cref="TaskHostTaskComplete"/> class.
    /// </summary>
    /// <param name="result">The result of the task's execution.</param>
    /// <param name="buildProcessEnvironment">The build process environment as it was at the end of the task's execution.</param>
    public TaskHostTaskComplete(OutOfProcTaskHostTaskResult result, IDictionary<string, string> buildProcessEnvironment)
    {
        Assumed.NotNull(result);

        _taskResult = result.Result;
        _taskException = result.TaskException;
        _taskExceptionMessage = result.ExceptionMessage;
        _taskExceptionMessageArgs = result.ExceptionMessageArgs;

        if (result.FinalParameterValues != null)
        {
            _taskOutputParameters = new Dictionary<string, TaskParameter>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, object> parameter in result.FinalParameterValues)
            {
                _taskOutputParameters[parameter.Key] = parameter.Value as TaskParameter ?? new TaskParameter(parameter.Value);
            }
        }

        if (buildProcessEnvironment != null)
        {
            _buildProcessEnvironment = buildProcessEnvironment as Dictionary<string, string>;

            if (_buildProcessEnvironment == null)
            {
                _buildProcessEnvironment = new Dictionary<string, string>(buildProcessEnvironment);
            }
        }
    }

#if FEATURE_REPORTFILEACCESSES
    /// <summary>
    ///  Initializes a new instance of the <see cref="TaskHostTaskComplete"/> class with reported file accesses.
    /// </summary>
    /// <param name="result">The result of the task's execution.</param>
    /// <param name="fileAccessData">The file accesses reported by the task.</param>
    /// <param name="buildProcessEnvironment">The build process environment as it was at the end of the task's execution.</param>
    public TaskHostTaskComplete(
        OutOfProcTaskHostTaskResult result,
        List<FileAccessData> fileAccessData,
        IDictionary<string, string> buildProcessEnvironment)
        : this(result, buildProcessEnvironment)
    {
        _fileAccessData = fileAccessData;
    }
#endif

    /// <summary>
    /// For deserialization.
    /// </summary>
    private TaskHostTaskComplete()
    {
    }

    /// <summary>
    /// Result of the task's execution.
    /// </summary>
    public TaskCompleteType TaskResult
    {
        [DebuggerStepThrough]
        get
        { return _taskResult; }
    }

    /// <summary>
    /// If the task threw an exception during its initialization or execution,
    /// save it here.
    /// </summary>
    public Exception TaskException
    {
        [DebuggerStepThrough]
        get
        { return _taskException; }
    }

    /// <summary>
    /// If there's an additional message that should be attached to the error
    /// logged beyond "task X failed unexpectedly", put it here.  May be null.
    /// </summary>
    public string TaskExceptionMessage
    {
        [DebuggerStepThrough]
        get
        { return _taskExceptionMessage; }
    }

    /// <summary>
    /// If there are arguments that need to be formatted into the message being
    /// sent, set them here.  May be null.
    /// </summary>
    public string[] TaskExceptionMessageArgs
    {
        [DebuggerStepThrough]
        get
        { return _taskExceptionMessageArgs; }
    }

    /// <summary>
    /// Task parameters and their values after the task has finished.
    /// </summary>
    public Dictionary<string, TaskParameter> TaskOutputParameters
    {
        [DebuggerStepThrough]
        get
        {
            if (_taskOutputParameters == null)
            {
                _taskOutputParameters = new Dictionary<string, TaskParameter>(StringComparer.OrdinalIgnoreCase);
            }

            return _taskOutputParameters;
        }
    }

    /// <summary>
    /// The process environment.
    /// </summary>
    public Dictionary<string, string> BuildProcessEnvironment
    {
        [DebuggerStepThrough]
        get
        { return _buildProcessEnvironment; }
    }

    /// <summary>
    /// How the build process environment is transferred on the wire. See <see cref="InvariantPayloadTransferMode"/>.
    /// Set by the task host when the negotiated packet version supports environment delta transfer and the task
    /// did not change the environment.
    /// </summary>
    internal InvariantPayloadTransferMode EnvironmentMode
    {
        [DebuggerStepThrough]
        get { return _environmentMode; }
        [DebuggerStepThrough]
        set { _environmentMode = value; }
    }

    /// <summary>
    /// The type of this packet.
    /// </summary>
    public NodePacketType Type
    {
        get { return NodePacketType.TaskHostTaskComplete; }
    }

#if FEATURE_REPORTFILEACCESSES
    /// <summary>
    /// Gets the file accesses reported by the task.
    /// </summary>
    public List<FileAccessData> FileAccessData
    {
        [DebuggerStepThrough]
        get => _fileAccessData;
    }
#endif

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
        TranslateBuildProcessEnvironment(translator);
#if FEATURE_REPORTFILEACCESSES
        translator.Translate(ref _fileAccessData,
            static (ITranslator translator, ref FileAccessData data) =>
            {
                ITranslatable translatable = data;
                translatable.Translate(translator);
                // FileAccessData is a struct; preserve the changes made to the boxed value during deserialization.
                data = (FileAccessData)translatable;
            });
#else
        bool hasFileAccessData = false;
        translator.Translate(ref hasFileAccessData);
#endif
    }

    private void TranslateBuildProcessEnvironment(ITranslator translator)
    {
        if (translator.NegotiatedPacketVersion >= NodePacketTypeExtensions.EnvironmentDeltaMinVersion)
        {
            translator.TranslateEnum(ref _environmentMode, (int)_environmentMode);

            if (_environmentMode == InvariantPayloadTransferMode.Full)
            {
                translator.TranslateDictionary(ref _buildProcessEnvironment, StringComparer.OrdinalIgnoreCase);
            }
        }
        else
        {
            translator.TranslateDictionary(ref _buildProcessEnvironment, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Factory for deserialization.
    /// </summary>
    internal static INodePacket FactoryForDeserialization(ITranslator translator)
    {
        TaskHostTaskComplete taskComplete = new TaskHostTaskComplete();
        taskComplete.Translate(translator);
        return taskComplete;
    }
}
