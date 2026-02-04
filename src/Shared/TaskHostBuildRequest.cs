// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !CLR2COMPATIBILITY

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Request packet from TaskHost to parent for BuildProjectFile* callbacks.
    /// Supports IBuildEngine, IBuildEngine2, and IBuildEngine3 variants.
    /// </summary>
    internal sealed class TaskHostBuildRequest : INodePacket, ITaskHostCallbackPacket
    {
        private int _requestId;
        private BuildRequestVariant _variant;

        // Single project parameters (IBuildEngine.BuildProjectFile, IBuildEngine2.BuildProjectFile)
        private string _projectFileName;
        private string[] _targetNames;
        private Dictionary<string, string> _globalProperties;
        private string _toolsVersion;

        // Multiple projects parameters (IBuildEngine2/3.BuildProjectFilesInParallel)
        private string[] _projectFileNames;
        private Dictionary<string, string>[] _globalPropertiesArray;
        private string[] _toolsVersions;

        // IBuildEngine2.BuildProjectFilesInParallel specific
        private bool _useResultsCache;
        private bool _unloadProjectsOnCompletion;

        // IBuildEngine3.BuildProjectFilesInParallel specific
        private List<string>[] _removeGlobalProperties;
        private bool _returnTargetOutputs;

        /// <summary>
        /// Constructor for deserialization.
        /// </summary>
        public TaskHostBuildRequest()
        {
        }

        /// <summary>
        /// IBuildEngine.BuildProjectFile (4 params) - projectFileName, targetNames, globalProperties, targetOutputs
        /// </summary>
        public static TaskHostBuildRequest CreateBuildEngine1Request(
            string projectFileName,
            string[] targetNames,
            IDictionary globalProperties)
        {
            return new TaskHostBuildRequest
            {
                _variant = BuildRequestVariant.BuildEngine1,
                _projectFileName = projectFileName,
                _targetNames = targetNames,
                _globalProperties = ConvertToDictionary(globalProperties),
            };
        }

        /// <summary>
        /// IBuildEngine2.BuildProjectFile (5 params) - adds toolsVersion
        /// </summary>
        public static TaskHostBuildRequest CreateBuildEngine2SingleRequest(
            string projectFileName,
            string[] targetNames,
            IDictionary globalProperties,
            string toolsVersion)
        {
            return new TaskHostBuildRequest
            {
                _variant = BuildRequestVariant.BuildEngine2Single,
                _projectFileName = projectFileName,
                _targetNames = targetNames,
                _globalProperties = ConvertToDictionary(globalProperties),
                _toolsVersion = toolsVersion,
            };
        }

        /// <summary>
        /// IBuildEngine2.BuildProjectFilesInParallel (7 params)
        /// </summary>
        public static TaskHostBuildRequest CreateBuildEngine2ParallelRequest(
            string[] projectFileNames,
            string[] targetNames,
            IDictionary[] globalProperties,
            string[] toolsVersions,
            bool useResultsCache,
            bool unloadProjectsOnCompletion)
        {
            return new TaskHostBuildRequest
            {
                _variant = BuildRequestVariant.BuildEngine2Parallel,
                _projectFileNames = projectFileNames,
                _targetNames = targetNames,
                _globalPropertiesArray = ConvertToDictionaryArray(globalProperties),
                _toolsVersions = toolsVersions,
                _useResultsCache = useResultsCache,
                _unloadProjectsOnCompletion = unloadProjectsOnCompletion,
            };
        }

        /// <summary>
        /// IBuildEngine3.BuildProjectFilesInParallel (6 params)
        /// </summary>
        public static TaskHostBuildRequest CreateBuildEngine3ParallelRequest(
            string[] projectFileNames,
            string[] targetNames,
            IDictionary[] globalProperties,
            IList<string>[] removeGlobalProperties,
            string[] toolsVersions,
            bool returnTargetOutputs)
        {
            return new TaskHostBuildRequest
            {
                _variant = BuildRequestVariant.BuildEngine3Parallel,
                _projectFileNames = projectFileNames,
                _targetNames = targetNames,
                _globalPropertiesArray = ConvertToDictionaryArray(globalProperties),
                _removeGlobalProperties = ConvertToListArray(removeGlobalProperties),
                _toolsVersions = toolsVersions,
                _returnTargetOutputs = returnTargetOutputs,
            };
        }

        #region Properties

        public NodePacketType Type => NodePacketType.TaskHostBuildRequest;

        public int RequestId
        {
            get => _requestId;
            set => _requestId = value;
        }

        public BuildRequestVariant Variant => _variant;

        // Single project
        public string ProjectFileName => _projectFileName;
        public string[] TargetNames => _targetNames;
        public Dictionary<string, string> GlobalProperties => _globalProperties;
        public string ToolsVersion => _toolsVersion;

        // Multiple projects
        public string[] ProjectFileNames => _projectFileNames;
        public Dictionary<string, string>[] GlobalPropertiesArray => _globalPropertiesArray;
        public string[] ToolsVersions => _toolsVersions;
        public List<string>[] RemoveGlobalProperties => _removeGlobalProperties;
        public bool UseResultsCache => _useResultsCache;
        public bool UnloadProjectsOnCompletion => _unloadProjectsOnCompletion;
        public bool ReturnTargetOutputs => _returnTargetOutputs;

        #endregion

        #region Serialization

        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            var packet = new TaskHostBuildRequest();
            packet.Translate(translator);
            return packet;
        }

        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _requestId);
            translator.TranslateEnum(ref _variant, (int)_variant);

            switch (_variant)
            {
                case BuildRequestVariant.BuildEngine1:
                    TranslateSingleProject(translator, includeToolsVersion: false);
                    break;

                case BuildRequestVariant.BuildEngine2Single:
                    TranslateSingleProject(translator, includeToolsVersion: true);
                    break;

                case BuildRequestVariant.BuildEngine2Parallel:
                    TranslateMultipleProjects(translator);
                    translator.Translate(ref _useResultsCache);
                    translator.Translate(ref _unloadProjectsOnCompletion);
                    break;

                case BuildRequestVariant.BuildEngine3Parallel:
                    TranslateMultipleProjects(translator);
                    TranslateRemoveGlobalProperties(translator);
                    translator.Translate(ref _returnTargetOutputs);
                    break;

                default:
                    ErrorUtilities.ThrowInternalErrorUnreachable();
                    break;
            }
        }

        private void TranslateSingleProject(ITranslator translator, bool includeToolsVersion)
        {
            translator.Translate(ref _projectFileName);
            translator.Translate(ref _targetNames);
            translator.TranslateDictionary(ref _globalProperties, StringComparer.OrdinalIgnoreCase);

            if (includeToolsVersion)
            {
                translator.Translate(ref _toolsVersion);
            }
        }

        private void TranslateMultipleProjects(ITranslator translator)
        {
            translator.Translate(ref _projectFileNames);
            translator.Translate(ref _targetNames);
            translator.Translate(ref _toolsVersions);

            // Translate array of dictionaries
            int count = _globalPropertiesArray?.Length ?? 0;
            translator.Translate(ref count);

            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                _globalPropertiesArray = count > 0 ? new Dictionary<string, string>[count] : null;
            }

            for (int i = 0; i < count; i++)
            {
                translator.TranslateDictionary(ref _globalPropertiesArray[i], StringComparer.OrdinalIgnoreCase);
            }
        }

        private void TranslateRemoveGlobalProperties(ITranslator translator)
        {
            int count = _removeGlobalProperties?.Length ?? 0;
            translator.Translate(ref count);

            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                _removeGlobalProperties = count > 0 ? new List<string>[count] : null;
            }

            for (int i = 0; i < count; i++)
            {
                List<string> list = _removeGlobalProperties?[i];
                translator.Translate(ref list);
                if (_removeGlobalProperties != null)
                {
                    _removeGlobalProperties[i] = list;
                }
            }
        }

        #endregion

        #region Helper Methods

        private static Dictionary<string, string> ConvertToDictionary(IDictionary source)
        {
            if (source == null)
            {
                return null;
            }

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in source)
            {
                result[entry.Key?.ToString() ?? string.Empty] = entry.Value?.ToString() ?? string.Empty;
            }
            return result;
        }

        private static Dictionary<string, string>[] ConvertToDictionaryArray(IDictionary[] source)
        {
            if (source == null)
            {
                return null;
            }

            var result = new Dictionary<string, string>[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                result[i] = ConvertToDictionary(source[i]);
            }
            return result;
        }

        private static List<string>[] ConvertToListArray(IList<string>[] source)
        {
            if (source == null)
            {
                return null;
            }

            var result = new List<string>[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                result[i] = source[i] != null ? new List<string>(source[i]) : null;
            }
            return result;
        }

        #endregion

        /// <summary>
        /// Identifies which BuildProjectFile* variant this request represents.
        /// </summary>
        internal enum BuildRequestVariant
        {
            /// <summary>IBuildEngine.BuildProjectFile (4 params)</summary>
            BuildEngine1 = 0,

            /// <summary>IBuildEngine2.BuildProjectFile (5 params - adds toolsVersion)</summary>
            BuildEngine2Single = 1,

            /// <summary>IBuildEngine2.BuildProjectFilesInParallel (7 params)</summary>
            BuildEngine2Parallel = 2,

            /// <summary>IBuildEngine3.BuildProjectFilesInParallel (6 params - returns BuildEngineResult)</summary>
            BuildEngine3Parallel = 3,
        }
    }
}

#endif
