using Microsoft.Build.Framework;

namespace Microsoft.Build.Tasks
{
    internal class ResolveAssemblyReferenceTaskOutput
    {
        internal ITaskItem[] CopyLocalFiles { get; set; }

        internal string DependsOnNETStandard { get; set; }

        internal string DependsOnSystemRuntime { get; set; }

        internal ITaskItem[] FilesWritten { get; set; }

        internal ITaskItem[] RelatedFiles { get; set; }

        internal ITaskItem[] ResolvedDependencyFiles { get; set; }

        internal ITaskItem[] ResolvedFiles { get; set; }

        internal ITaskItem[] SatelliteFiles { get; set; }

        internal ITaskItem[] ScatterFiles { get; set; }

        internal ITaskItem[] SerializationAssemblyFiles { get; set; }

        internal ITaskItem[] SuggestedRedirects { get; set; }
    }
}
