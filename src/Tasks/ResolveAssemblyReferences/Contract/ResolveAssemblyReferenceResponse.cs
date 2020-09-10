// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract
{
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

        public ReadOnlyTaskItem[] CopyLocalFiles { get; set; }

        public string DependsOnNETStandard { get; set; }

        public string DependsOnSystemRuntime { get; set; }

        public ReadOnlyTaskItem[] FilesWritten { get; set; }

        public ReadOnlyTaskItem[] RelatedFiles { get; set; }

        public ReadOnlyTaskItem[] ResolvedDependencyFiles { get; set; }

        public ReadOnlyTaskItem[] ResolvedFiles { get; set; }

        public ReadOnlyTaskItem[] SatelliteFiles { get; set; }

        public ReadOnlyTaskItem[] ScatterFiles { get; set; }

        public ReadOnlyTaskItem[] SerializationAssemblyFiles { get; set; }

        public ReadOnlyTaskItem[] SuggestedRedirects { get; set; }
    }
}
