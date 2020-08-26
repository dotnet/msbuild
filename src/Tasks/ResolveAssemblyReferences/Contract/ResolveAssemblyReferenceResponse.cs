// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract
{
    [MessagePackObject]
    internal sealed class ResolveAssemblyReferenceResponse
    {
        public ResolveAssemblyReferenceResponse()
        {
        }

        internal ResolveAssemblyReferenceResponse(ResolveAssemblyReferenceTaskOutput output)
        {
            CopyLocalFiles = ReadOnlyTaskItem.CreateArray(output.CopyLocalFiles);
            DependsOnNETStandard = output.DependsOnNETStandard;
            DependsOnSystemRuntime = output.DependsOnSystemRuntime;
            FilesWritten = ReadOnlyTaskItem.CreateArray(output.FilesWritten);
            RelatedFiles = ReadOnlyTaskItem.CreateArray(output.RelatedFiles);
            ResolvedDependencyFiles = ReadOnlyTaskItem.CreateArray(output.ResolvedDependencyFiles);
            ResolvedFiles = ReadOnlyTaskItem.CreateArray(output.ResolvedFiles);
            SatelliteFiles = ReadOnlyTaskItem.CreateArray(output.SatelliteFiles);
            ScatterFiles = ReadOnlyTaskItem.CreateArray(output.ScatterFiles);
            SerializationAssemblyFiles = ReadOnlyTaskItem.CreateArray(output.SerializationAssemblyFiles);
            SuggestedRedirects = ReadOnlyTaskItem.CreateArray(output.SuggestedRedirects);
        }

        [Key(0)]
        public ReadOnlyTaskItem[] CopyLocalFiles { get; set; }

        [Key(1)]
        public string DependsOnNETStandard { get; set; }

        [Key(2)]
        public string DependsOnSystemRuntime { get; set; }

        [Key(3)]
        public ReadOnlyTaskItem[] FilesWritten { get; set; }

        [Key(4)]
        public ReadOnlyTaskItem[] RelatedFiles { get; set; }

        [Key(5)]
        public ReadOnlyTaskItem[] ResolvedDependencyFiles { get; set; }

        [Key(6)]
        public ReadOnlyTaskItem[] ResolvedFiles { get; set; }

        [Key(7)]
        public ReadOnlyTaskItem[] SatelliteFiles { get; set; }

        [Key(8)]
        public ReadOnlyTaskItem[] ScatterFiles { get; set; }

        [Key(9)]
        public ReadOnlyTaskItem[] SerializationAssemblyFiles { get; set; }

        [Key(10)]
        public ReadOnlyTaskItem[] SuggestedRedirects { get; set; }
    }
}
