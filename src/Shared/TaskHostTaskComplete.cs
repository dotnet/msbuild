// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
#if !CLR2COMPATIBILITY
using Microsoft.Build.Experimental.FileAccess;
#endif
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// How the task completed -- successful, failed, or crashed
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


#pragma warning disable CS1572 // XML comment has a param tag, but there is no parameter by that name. Justification: xmldoc doesn't seem to interact well with #ifdef of params.
        /// <summary>
        /// Initializes a new instance of the <see cref="TaskHostTaskComplete"/> class.
        /// </summary>
        /// <param name="result">The result of the task's execution.</param>
        /// <param name="fileAccessData">The file accesses reported by the task.</param>
        /// <param name="buildProcessEnvironment">The build process environment as it was at the end of the task's execution.</param>
#pragma warning restore CS1572 // XML comment has a param tag, but there is no parameter by that name
        public TaskHostTaskComplete(
            OutOfProcTaskHostTaskResult result,
#if FEATURE_REPORTFILEACCESSES
            List<FileAccessData> fileAccessData,
#endif
            IDictionary<string, string> buildProcessEnvironment)
        {
            ErrorUtilities.VerifyThrowInternalNull(result, nameof(result));

            _taskResult = result.Result;
            _taskException = result.TaskException;
            _taskExceptionMessage = result.ExceptionMessage;
            _taskExceptionMessageArgs = result.ExceptionMessageArgs;
#if FEATURE_REPORTFILEACCESSES
            _fileAccessData = fileAccessData;
#endif

            if (result.FinalParameterValues != null)
            {
                _taskOutputParameters = new Dictionary<string, TaskParameter>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, object> parameter in result.FinalParameterValues)
                {
                    _taskOutputParameters[parameter.Key] = new TaskParameter(parameter.Value);
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
            translator.TranslateDictionary(ref _buildProcessEnvironment, StringComparer.OrdinalIgnoreCase);
#if FEATURE_REPORTFILEACCESSES
            translator.Translate(ref _fileAccessData,
                (ITranslator translator, ref FileAccessData data) => ((ITranslatable)data).Translate(translator));
#else
            bool hasFileAccessData = false;
            translator.Translate(ref hasFileAccessData);
#endif
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
}
