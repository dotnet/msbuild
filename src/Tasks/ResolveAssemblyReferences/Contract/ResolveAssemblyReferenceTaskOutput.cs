// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract
{
    internal sealed class ResolveAssemblyReferenceTaskOutput
    {
        public ResolveAssemblyReferenceTaskOutput()
        {
        }

        public ResolveAssemblyReferenceTaskOutput(ResolveAssemblyReferenceResponse response)
        {
            CopyLocalFiles = ReadOnlyTaskItem.ToTaskItem(response.CopyLocalFiles);
            DependsOnNETStandard = response.DependsOnNETStandard;
            DependsOnSystemRuntime = response.DependsOnSystemRuntime;
            FilesWritten = ReadOnlyTaskItem.ToTaskItem(response.FilesWritten);
            RelatedFiles = ReadOnlyTaskItem.ToTaskItem(response.RelatedFiles);
            ResolvedDependencyFiles = ReadOnlyTaskItem.ToTaskItem(response.ResolvedDependencyFiles);
            ResolvedFiles = ReadOnlyTaskItem.ToTaskItem(response.ResolvedFiles);
            SatelliteFiles = ReadOnlyTaskItem.ToTaskItem(response.SatelliteFiles);
            ScatterFiles = ReadOnlyTaskItem.ToTaskItem(response.ScatterFiles);
            SerializationAssemblyFiles = ReadOnlyTaskItem.ToTaskItem(response.SerializationAssemblyFiles);
            SuggestedRedirects = ReadOnlyTaskItem.ToTaskItem(response.SuggestedRedirects);
        }

        public ITaskItem[] CopyLocalFiles { get; set; }

        public string DependsOnNETStandard { get; set; }

        public string DependsOnSystemRuntime { get; set; }

        public ITaskItem[] FilesWritten { get; set; }

        public ITaskItem[] RelatedFiles { get; set; }

        public ITaskItem[] ResolvedDependencyFiles { get; set; }

        public ITaskItem[] ResolvedFiles { get; set; }

        public ITaskItem[] SatelliteFiles { get; set; }

        public ITaskItem[] ScatterFiles { get; set; }

        public ITaskItem[] SerializationAssemblyFiles { get; set; }

        public ITaskItem[] SuggestedRedirects { get; set; }
    }
}
