// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using ParameterType = Microsoft.Build.Tasks.AssemblyDependency.RarTaskParameters.ParameterType;

namespace Microsoft.Build.Tasks.AssemblyDependency
{
    /// <summary>
    /// Extracts and hydrates task inputs for a ResolveAssemblyReference.
    /// </summary>
    internal sealed class RarNodeExecuteRequest : INodePacket
    {
        private Dictionary<string, TaskParameter> _taskInputs = new(StringComparer.Ordinal);
        private int _lineNumberOfTaskNode;
        private int _columnNumberOfTaskNode;
        private string? _projectFileOfTaskNode;
        private MessageImportance _minimumMessageImportance;
        private bool _isTaskInputLoggingEnabled;

        internal RarNodeExecuteRequest(ResolveAssemblyReference rar)
        {
            // The RAR node may have a different working directory than the target, so convert potential relative paths to absolute.
            if (rar.AppConfigFile != null)
            {
                rar.AppConfigFile = Path.GetFullPath(rar.AppConfigFile);
            }

            if (rar.StateFile != null)
            {
                rar.StateFile = Path.GetFullPath(rar.StateFile);
            }

            _taskInputs = RarTaskParameters.Get(ParameterType.Input, rar);

            // Ensure log messages are identical to those that would be produced on the client.
            _lineNumberOfTaskNode = rar.BuildEngine.LineNumberOfTaskNode;
            _columnNumberOfTaskNode = rar.BuildEngine.ColumnNumberOfTaskNode;
            _projectFileOfTaskNode = rar.BuildEngine.ProjectFileOfTaskNode;
            _minimumMessageImportance = rar.Log.LogsMessagesOfImportance(MessageImportance.Low) ? MessageImportance.Low
                    : rar.Log.LogsMessagesOfImportance(MessageImportance.Normal) ? MessageImportance.Normal
                    : MessageImportance.High;
            _isTaskInputLoggingEnabled = rar.Log.IsTaskInputLoggingEnabled;
        }

        internal RarNodeExecuteRequest(ITranslator translator) => Translate(translator);

        public NodePacketType Type => NodePacketType.RarNodeExecuteRequest;

        public void Translate(ITranslator translator)
        {
            // TODO: The main outputs (e.g. ResolvedFiles, ResolvedDependencyFiles) will go through a different TaskItem
            // serialization path. Sequential items in each output type share the same set of metadata keys and similar
            // values, so we can save overhead + pre-compute the CopyLocal set by breaking out of TaskParameter.
            translator.TranslateDictionary(ref _taskInputs, StringComparer.Ordinal, TaskParameter.FactoryForDeserialization);
            translator.Translate(ref _lineNumberOfTaskNode);
            translator.Translate(ref _columnNumberOfTaskNode);
            translator.Translate(ref _projectFileOfTaskNode);
            translator.TranslateEnum(ref _minimumMessageImportance, (int)_minimumMessageImportance);
            translator.Translate(ref _isTaskInputLoggingEnabled);
        }

        internal void SetTaskInputs(ResolveAssemblyReference rar, RarNodeBuildEngine buildEngine)
        {
            buildEngine.Setup(
                _lineNumberOfTaskNode,
                _columnNumberOfTaskNode,
                _projectFileOfTaskNode,
                _minimumMessageImportance,
                _isTaskInputLoggingEnabled);

            RarTaskParameters.Set(ParameterType.Input, rar, _taskInputs);
            rar.AllowOutOfProcNode = false;
            rar.BuildEngine = buildEngine;
        }
    }
}
