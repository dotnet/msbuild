// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using ParameterType = Microsoft.Build.Tasks.AssemblyDependency.RarTaskParameters.ParameterType;

namespace Microsoft.Build.Tasks.AssemblyDependency
{
    /// <summary>
    /// Extracts and hydrates task outputs for ResolveAssemblyReference.
    /// </summary>
    internal sealed class RarNodeExecuteResponse : INodePacket
    {
        private Dictionary<string, TaskParameter> _taskOutputs = new(StringComparer.Ordinal);
        private int _numCopyLocalFiles;
        private bool _success;

        internal RarNodeExecuteResponse(ResolveAssemblyReference rar, bool success)
        {
            _taskOutputs = RarTaskParameters.Get(ParameterType.Output, rar);
            _numCopyLocalFiles = rar.CopyLocalFiles.Length;
            _success = success;
        }

        internal RarNodeExecuteResponse(ITranslator translator) => Translate(translator);

        public NodePacketType Type => NodePacketType.RarNodeExecuteResponse;

        internal bool Success => _success;

        public void Translate(ITranslator translator)
        {
            // TODO: The main outputs (e.g. ResolvedFiles, ResolvedDependencyFiles) will go through a different TaskItem
            // serialization path. Sequential items in each output share the same set of metadata keys and similar values,
            // so we can save a ton of overhead (and precompute the CopyLocal check) by breaking out of TaskParameter.
            translator.TranslateDictionary(ref _taskOutputs, StringComparer.Ordinal, TaskParameter.FactoryForDeserialization);
            translator.Translate(ref _numCopyLocalFiles);
            translator.Translate(ref _success);
        }

        internal void SetTaskOutputs(ResolveAssemblyReference rar)
        {
            RarTaskParameters.Set(ParameterType.Output, rar, _taskOutputs);

            if (_numCopyLocalFiles == 0)
            {
                return;
            }

            // CopyLocalFiles consists of a list of references. Although the reference equality itself doesn't matter since
            // the engine will later create copies, we can skip serializing these and reconstruct them on the client.
            int i = 0;
            ITaskItem[] copyLocalFiles = new ITaskItem[_numCopyLocalFiles];
            FindCopyLocalFiles(copyLocalFiles, ref i, rar.ResolvedFiles);
            FindCopyLocalFiles(copyLocalFiles, ref i, rar.ResolvedDependencyFiles);
            FindCopyLocalFiles(copyLocalFiles, ref i, rar.RelatedFiles);
            FindCopyLocalFiles(copyLocalFiles, ref i, rar.SatelliteFiles);
            FindCopyLocalFiles(copyLocalFiles, ref i, rar.SerializationAssemblyFiles);
            FindCopyLocalFiles(copyLocalFiles, ref i, rar.ScatterFiles);

            static void FindCopyLocalFiles(ITaskItem[] copyLocalFiles, ref int i, ITaskItem[] items)
            {
                foreach (ITaskItem taskItem in items)
                {
                    // Prefer ITaskItem2 as it skips escaping checks. This should always be true coming from RAR.
                    string isCopyLocal = taskItem is ITaskItem2 taskItem2
                        ? taskItem2.GetMetadataValueEscaped(ItemMetadataNames.copyLocal)
                        : taskItem.GetMetadata(ItemMetadataNames.copyLocal);

                    if (string.Equals(isCopyLocal, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        copyLocalFiles[i] = taskItem;
                        i++;
                    }
                }
            }

            rar.CopyLocalFiles = copyLocalFiles;
        }
    }
}
