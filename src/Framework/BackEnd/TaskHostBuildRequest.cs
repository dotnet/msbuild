// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Packet sent from TaskHost to owning worker node to execute a BuildProjectFile* callback.
    /// All four BuildProjectFile/BuildProjectFilesInParallel overloads normalize to the
    /// IBuildEngine3 6-param canonical form carried by this packet.
    /// </summary>
    internal class TaskHostBuildRequest : INodePacket, ITaskHostCallbackPacket
    {
        private int _requestId;
        private string[]? _projectFileNames;
        private string[]? _targetNames;
        private Dictionary<string, string>?[]? _globalProperties;
        private List<string>?[]? _removeGlobalProperties;
        private string[]? _toolsVersions;
        private bool _returnTargetOutputs;

        public TaskHostBuildRequest()
        {
        }

        public TaskHostBuildRequest(
            string[]? projectFileNames,
            string[]? targetNames,
            Dictionary<string, string>?[]? globalProperties,
            List<string>?[]? removeGlobalProperties,
            string[]? toolsVersions,
            bool returnTargetOutputs)
        {
            _projectFileNames = projectFileNames;
            _targetNames = targetNames;
            _globalProperties = globalProperties;
            _removeGlobalProperties = removeGlobalProperties;
            _toolsVersions = toolsVersions;
            _returnTargetOutputs = returnTargetOutputs;
        }

        public NodePacketType Type => NodePacketType.TaskHostBuildRequest;

        public int RequestId
        {
            get => _requestId;
            set => _requestId = value;
        }

        /// <summary>Array of project file paths to build.</summary>
        public string[]? ProjectFileNames => _projectFileNames;

        /// <summary>Array of target names to build in each project.</summary>
        public string[]? TargetNames => _targetNames;

        /// <summary>Per-project global properties to pass to the build.</summary>
        public Dictionary<string, string>?[]? GlobalProperties => _globalProperties;

        /// <summary>Per-project global properties to remove before building.</summary>
        public List<string>?[]? RemoveGlobalProperties => _removeGlobalProperties;

        /// <summary>Per-project tools versions to use.</summary>
        public string[]? ToolsVersions => _toolsVersions;

        /// <summary>Whether to include target outputs in the response.</summary>
        public bool ReturnTargetOutputs => _returnTargetOutputs;

        /// <summary>
        /// Converts non-generic IDictionary[] (as used by IBuildEngine interfaces) to
        /// Dictionary&lt;string, string&gt;[] for serialization.
        /// </summary>
        internal static Dictionary<string, string>?[]? ConvertGlobalProperties(IDictionary[]? globalProperties)
        {
            if (globalProperties is null)
            {
                return null;
            }

            var result = new Dictionary<string, string>?[globalProperties.Length];
            for (int i = 0; i < globalProperties.Length; i++)
            {
                if (globalProperties[i] is not null)
                {
                    result[i] = new Dictionary<string, string>(globalProperties[i].Count, StringComparer.OrdinalIgnoreCase);
                    foreach (DictionaryEntry entry in globalProperties[i])
                    {
                        result[i]![(string)entry.Key] = entry.Value?.ToString() ?? string.Empty;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Converts IList&lt;string&gt;[] to List&lt;string&gt;[] for serialization.
        /// </summary>
        internal static List<string>?[]? ConvertRemoveGlobalProperties(IList<string>[]? removeGlobalProperties)
        {
            if (removeGlobalProperties is null)
            {
                return null;
            }

            return Array.ConvertAll(removeGlobalProperties,
                list => list is not null ? new List<string>(list) : null);
        }

        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _requestId);
            TranslateNullableStringArray(translator, ref _projectFileNames);
            TranslateNullableStringArray(translator, ref _targetNames);
            translator.Translate(ref _returnTargetOutputs);
            TranslateNullableStringArray(translator, ref _toolsVersions);
            TranslateGlobalPropertiesArray(translator);
            TranslateRemoveGlobalPropertiesArray(translator);
        }

        /// <summary>
        /// Serializes a string array where individual elements may be null.
        /// The standard translator.Translate(ref string[]) doesn't handle null elements.
        /// </summary>
        private static void TranslateNullableStringArray(ITranslator translator, ref string[]? array)
        {
            bool hasArray = array is not null;
            translator.Translate(ref hasArray);

            if (!hasArray)
            {
                array = null;
                return;
            }

            int length = array?.Length ?? 0;
            translator.Translate(ref length);

            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                array = new string[length];
            }

            for (int i = 0; i < length; i++)
            {
                string? element = array![i];
                translator.Translate(ref element);
                array[i] = element!;
            }
        }

        private void TranslateGlobalPropertiesArray(ITranslator translator)
        {
            bool hasArray = _globalProperties is not null;
            translator.Translate(ref hasArray);

            if (!hasArray)
            {
                _globalProperties = null;
                return;
            }

            int length = _globalProperties?.Length ?? 0;
            translator.Translate(ref length);

            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                _globalProperties = new Dictionary<string, string>?[length];
            }

            for (int i = 0; i < length; i++)
            {
                Dictionary<string, string>? dict = _globalProperties![i];
                translator.TranslateDictionary(ref dict, StringComparer.OrdinalIgnoreCase);
                _globalProperties[i] = dict;
            }
        }

        private void TranslateRemoveGlobalPropertiesArray(ITranslator translator)
        {
            bool hasArray = _removeGlobalProperties is not null;
            translator.Translate(ref hasArray);

            if (!hasArray)
            {
                _removeGlobalProperties = null;
                return;
            }

            int length = _removeGlobalProperties?.Length ?? 0;
            translator.Translate(ref length);

            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                _removeGlobalProperties = new List<string>?[length];
            }

            for (int i = 0; i < length; i++)
            {
                List<string>? list = _removeGlobalProperties![i];
                translator.Translate(ref list);
                _removeGlobalProperties[i] = list;
            }
        }

        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            var packet = new TaskHostBuildRequest();
            packet.Translate(translator);
            return packet;
        }
    }
}
