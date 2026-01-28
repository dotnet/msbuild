// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !CLR2COMPATIBILITY

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Response packet from parent to TaskHost for BuildProjectFile* callbacks.
    /// Contains build success/failure and target outputs (ITaskItem arrays).
    /// </summary>
    internal sealed class TaskHostBuildResponse : INodePacket, ITaskHostCallbackPacket
    {
        private int _requestId;
        private bool _overallResult;

        // For single project results (IBuildEngine, IBuildEngine2 single)
        // Maps target name -> ITaskItem[]
        private Dictionary<string, ITaskItem[]> _targetOutputs;

        // For multiple projects results (IBuildEngine2/3 parallel)
        // List of dictionaries, one per project
        private List<Dictionary<string, ITaskItem[]>> _targetOutputsPerProject;

        /// <summary>
        /// Constructor for deserialization.
        /// </summary>
        public TaskHostBuildResponse()
        {
        }

        /// <summary>
        /// Constructor for single project result (IBuildEngine, IBuildEngine2 single).
        /// </summary>
        /// <param name="requestId">The request ID to correlate with the original request.</param>
        /// <param name="result">True if the build succeeded.</param>
        /// <param name="targetOutputs">Target outputs as IDictionary (target name -> ITaskItem[]).</param>
        public TaskHostBuildResponse(int requestId, bool result, IDictionary targetOutputs)
        {
            _requestId = requestId;
            _overallResult = result;
            _targetOutputs = ConvertTargetOutputs(targetOutputs);
        }

        /// <summary>
        /// Constructor for IBuildEngine2.BuildProjectFilesInParallel result.
        /// Note: IBuildEngine2 variant fills targetOutputsPerProject parameter by reference,
        /// but we treat it similarly to IBuildEngine3 for serialization.
        /// </summary>
        /// <param name="requestId">The request ID to correlate with the original request.</param>
        /// <param name="result">True if all builds succeeded.</param>
        /// <param name="targetOutputsPerProject">Target outputs per project.</param>
        public TaskHostBuildResponse(int requestId, bool result, IDictionary[] targetOutputsPerProject)
        {
            _requestId = requestId;
            _overallResult = result;
            _targetOutputsPerProject = ConvertTargetOutputsArray(targetOutputsPerProject);
        }

        /// <summary>
        /// Constructor for IBuildEngine3.BuildProjectFilesInParallel result.
        /// </summary>
        /// <param name="requestId">The request ID to correlate with the original request.</param>
        /// <param name="result">True if all builds succeeded.</param>
        /// <param name="targetOutputsPerProject">Target outputs per project from BuildEngineResult.</param>
        public TaskHostBuildResponse(
            int requestId,
            bool result,
            IList<IDictionary<string, ITaskItem[]>> targetOutputsPerProject)
        {
            _requestId = requestId;
            _overallResult = result;
            _targetOutputsPerProject = ConvertTypedTargetOutputsPerProject(targetOutputsPerProject);
        }

        #region Properties

        public NodePacketType Type => NodePacketType.TaskHostBuildResponse;

        public int RequestId
        {
            get => _requestId;
            set => _requestId = value;
        }

        public bool OverallResult => _overallResult;

        /// <summary>
        /// Gets target outputs for single project builds.
        /// Caller should populate the IDictionary passed to BuildProjectFile.
        /// </summary>
        public IDictionary GetTargetOutputsForSingleProject()
        {
            if (_targetOutputs == null)
            {
                return null;
            }

            // Return as Hashtable since that's what IBuildEngine expects
            var result = new Hashtable(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in _targetOutputs)
            {
                result[kvp.Key] = kvp.Value;
            }
            return result;
        }

        /// <summary>
        /// Gets target outputs per project for IBuildEngine2 parallel builds.
        /// </summary>
        public IDictionary[] GetTargetOutputsForParallelBuild()
        {
            if (_targetOutputsPerProject == null)
            {
                return null;
            }

            var result = new IDictionary[_targetOutputsPerProject.Count];
            for (int i = 0; i < _targetOutputsPerProject.Count; i++)
            {
                var dict = _targetOutputsPerProject[i];
                if (dict != null)
                {
                    var hashtable = new Hashtable(StringComparer.OrdinalIgnoreCase);
                    foreach (var kvp in dict)
                    {
                        hashtable[kvp.Key] = kvp.Value;
                    }
                    result[i] = hashtable;
                }
            }
            return result;
        }

        /// <summary>
        /// Gets target outputs per project for IBuildEngine3 parallel builds (BuildEngineResult).
        /// </summary>
        public List<IDictionary<string, ITaskItem[]>> GetTargetOutputsForBuildEngineResult()
        {
            if (_targetOutputsPerProject == null)
            {
                return null;
            }

            var result = new List<IDictionary<string, ITaskItem[]>>(_targetOutputsPerProject.Count);
            foreach (var dict in _targetOutputsPerProject)
            {
                if (dict != null)
                {
                    var typedDict = new Dictionary<string, ITaskItem[]>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kvp in dict)
                    {
                        typedDict[kvp.Key] = kvp.Value;
                    }
                    result.Add(typedDict);
                }
                else
                {
                    result.Add(null);
                }
            }
            return result;
        }

        #endregion

        #region Serialization

        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            var packet = new TaskHostBuildResponse();
            packet.Translate(translator);
            return packet;
        }

        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _requestId);
            translator.Translate(ref _overallResult);

            // Determine which format we have
            bool hasSingleOutputs = _targetOutputs != null;
            bool hasMultipleOutputs = _targetOutputsPerProject != null;

            translator.Translate(ref hasSingleOutputs);
            translator.Translate(ref hasMultipleOutputs);

            if (hasSingleOutputs)
            {
                TranslateTargetOutputs(translator, ref _targetOutputs);
            }

            if (hasMultipleOutputs)
            {
                TranslateTargetOutputsPerProject(translator);
            }
        }

        private void TranslateTargetOutputs(
            ITranslator translator,
            ref Dictionary<string, ITaskItem[]> outputs)
        {
            int count = outputs?.Count ?? 0;
            translator.Translate(ref count);

            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                outputs = new Dictionary<string, ITaskItem[]>(count, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < count; i++)
                {
                    string key = null;
                    translator.Translate(ref key);

                    ITaskItem[] items = null;
                    TranslateTaskItemArray(translator, ref items);

                    outputs[key] = items;
                }
            }
            else
            {
                foreach (var kvp in outputs)
                {
                    string key = kvp.Key;
                    translator.Translate(ref key);

                    ITaskItem[] items = kvp.Value;
                    TranslateTaskItemArray(translator, ref items);
                }
            }
        }

        private void TranslateTargetOutputsPerProject(ITranslator translator)
        {
            int projectCount = _targetOutputsPerProject?.Count ?? 0;
            translator.Translate(ref projectCount);

            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                _targetOutputsPerProject = new List<Dictionary<string, ITaskItem[]>>(projectCount);
                for (int i = 0; i < projectCount; i++)
                {
                    bool hasOutputs = false;
                    translator.Translate(ref hasOutputs);

                    if (hasOutputs)
                    {
                        Dictionary<string, ITaskItem[]> projectOutputs = null;
                        TranslateTargetOutputs(translator, ref projectOutputs);
                        _targetOutputsPerProject.Add(projectOutputs);
                    }
                    else
                    {
                        _targetOutputsPerProject.Add(null);
                    }
                }
            }
            else
            {
                foreach (var projectOutputs in _targetOutputsPerProject)
                {
                    bool hasOutputs = projectOutputs != null;
                    translator.Translate(ref hasOutputs);

                    if (hasOutputs)
                    {
                        var outputs = projectOutputs;
                        TranslateTargetOutputs(translator, ref outputs);
                    }
                }
            }
        }

        /// <summary>
        /// Translates an ITaskItem[] using TaskParameter for proper serialization.
        /// </summary>
        private static void TranslateTaskItemArray(ITranslator translator, ref ITaskItem[] items)
        {
            // Use TaskParameter which handles ITaskItem[] serialization correctly
            if (translator.Mode == TranslationDirection.WriteToStream)
            {
                var taskParam = new TaskParameter(items);
                taskParam.Translate(translator);
            }
            else
            {
                var taskParam = TaskParameter.FactoryForDeserialization(translator);
                items = taskParam.WrappedParameter as ITaskItem[];
            }
        }

        #endregion

        #region Helper Methods

        private static Dictionary<string, ITaskItem[]> ConvertTargetOutputs(IDictionary source)
        {
            if (source == null)
            {
                return null;
            }

            var result = new Dictionary<string, ITaskItem[]>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in source)
            {
                string key = entry.Key?.ToString() ?? string.Empty;
                result[key] = entry.Value as ITaskItem[];
            }
            return result;
        }

        private static List<Dictionary<string, ITaskItem[]>> ConvertTargetOutputsArray(IDictionary[] source)
        {
            if (source == null)
            {
                return null;
            }

            var result = new List<Dictionary<string, ITaskItem[]>>(source.Length);
            foreach (var dict in source)
            {
                result.Add(ConvertTargetOutputs(dict));
            }
            return result;
        }

        private static List<Dictionary<string, ITaskItem[]>> ConvertTypedTargetOutputsPerProject(
            IList<IDictionary<string, ITaskItem[]>> source)
        {
            if (source == null)
            {
                return null;
            }

            var result = new List<Dictionary<string, ITaskItem[]>>(source.Count);
            foreach (var dict in source)
            {
                if (dict != null)
                {
                    var converted = new Dictionary<string, ITaskItem[]>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kvp in dict)
                    {
                        converted[kvp.Key] = kvp.Value;
                    }
                    result.Add(converted);
                }
                else
                {
                    result.Add(null);
                }
            }
            return result;
        }

        #endregion
    }
}

#endif
