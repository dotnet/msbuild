// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Response packet from owning worker node to TaskHost with BuildProjectFile* results.
    /// Carries the build success/failure and target outputs per project.
    /// </summary>
    internal class TaskHostBuildResponse : INodePacket, ITaskHostCallbackPacket
    {
        private int _requestId;
        private bool _success;

        /// <summary>
        /// Target outputs per project. Each entry is a dictionary mapping target names to TaskParameter
        /// wrapping ITaskItem[] outputs. Uses the same TaskParameter serialization as TaskHostTaskComplete.
        /// </summary>
        private List<Dictionary<string, TaskParameter>>? _targetOutputsPerProject;

        public TaskHostBuildResponse()
        {
        }

        public TaskHostBuildResponse(int requestId, bool success, List<Dictionary<string, TaskParameter>>? targetOutputsPerProject)
        {
            _requestId = requestId;
            _success = success;
            _targetOutputsPerProject = targetOutputsPerProject;
        }

        public NodePacketType Type => NodePacketType.TaskHostBuildResponse;

        public int RequestId
        {
            get => _requestId;
            set => _requestId = value;
        }

        /// <summary>Whether the build succeeded.</summary>
        public bool Success => _success;

        /// <summary>Per-project target outputs, or null if outputs were not requested.</summary>
        public List<Dictionary<string, TaskParameter>>? TargetOutputsPerProject => _targetOutputsPerProject;

        /// <summary>
        /// Reconstructs a <see cref="BuildEngineResult"/> from this response packet.
        /// Converts <see cref="TaskParameter"/> values back to <see cref="ITaskItem"/>[] arrays.
        /// </summary>
        public BuildEngineResult ToBuildEngineResult()
        {
            List<IDictionary<string, ITaskItem[]>>? result = null;

            if (_targetOutputsPerProject is not null)
            {
                result = new List<IDictionary<string, ITaskItem[]>>(_targetOutputsPerProject.Count);

                foreach (Dictionary<string, TaskParameter> projectOutputs in _targetOutputsPerProject)
                {
                    var dict = new Dictionary<string, ITaskItem[]>(projectOutputs.Count, StringComparer.OrdinalIgnoreCase);

                    if (projectOutputs is not null)
                    {
                        foreach (KeyValuePair<string, TaskParameter> entry in projectOutputs)
                        {
                            dict[entry.Key] = (ITaskItem[]?)entry.Value?.WrappedParameter ?? [];
                        }
                    }

                    result.Add(dict);
                }
            }

            return new BuildEngineResult(_success, result ?? []);
        }

        /// <summary>
        /// Creates a response from a <see cref="BuildEngineResult"/>.
        /// Wraps <see cref="ITaskItem"/>[] arrays in <see cref="TaskParameter"/> for serialization.
        /// </summary>
        internal static TaskHostBuildResponse FromBuildEngineResult(int requestId, BuildEngineResult engineResult)
        {
            List<Dictionary<string, TaskParameter>>? outputs = null;

            if (engineResult.TargetOutputsPerProject is not null && engineResult.TargetOutputsPerProject.Count > 0)
            {
                outputs = new List<Dictionary<string, TaskParameter>>(engineResult.TargetOutputsPerProject.Count);

                foreach (IDictionary<string, ITaskItem[]> projectOutputs in engineResult.TargetOutputsPerProject)
                {
                    var dict = new Dictionary<string, TaskParameter>(projectOutputs?.Count ?? 0, StringComparer.OrdinalIgnoreCase);

                    if (projectOutputs is not null)
                    {
                        foreach (KeyValuePair<string, ITaskItem[]> entry in projectOutputs)
                        {
                            dict[entry.Key] = new TaskParameter(entry.Value);
                        }
                    }

                    outputs.Add(dict);
                }
            }

            return new TaskHostBuildResponse(requestId, engineResult.Result, outputs);
        }

        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _requestId);
            translator.Translate(ref _success);
            TranslateTargetOutputs(translator);
        }

        private void TranslateTargetOutputs(ITranslator translator)
        {
            bool hasOutputs = _targetOutputsPerProject is not null;
            translator.Translate(ref hasOutputs);

            if (!hasOutputs)
            {
                _targetOutputsPerProject = null;
                return;
            }

            int count = _targetOutputsPerProject?.Count ?? 0;
            translator.Translate(ref count);

            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                _targetOutputsPerProject = new List<Dictionary<string, TaskParameter>>(count);
                for (int i = 0; i < count; i++)
                {
                    Dictionary<string, TaskParameter>? dict = null;
                    translator.TranslateDictionary(ref dict, StringComparer.OrdinalIgnoreCase, TaskParameter.FactoryForDeserialization);
                    _targetOutputsPerProject.Add(dict!);
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    Dictionary<string, TaskParameter>? dict = _targetOutputsPerProject![i];
                    translator.TranslateDictionary(ref dict, StringComparer.OrdinalIgnoreCase, TaskParameter.FactoryForDeserialization);
                }
            }
        }

        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            var packet = new TaskHostBuildResponse();
            packet.Translate(translator);
            return packet;
        }
    }
}
