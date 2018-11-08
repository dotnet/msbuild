using System;
namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Domain
{
    // TODO: Find a clearer name, as it's really a TaskItem field on a ResolveAssemblyReferenceResponse,
    // but ResolveAssemblyReferenceResponseTaskItemField is quite a mouthful
    internal enum TaskItemField
    {
        CopyLocalFiles = 1,
        FilesWritten = 2,
        RelatedFiles = 4,
        ResolvedDependencyFiles = 8,
        ResolvedFiles = 16,
        SatelliteFiles = 32,
        ScatterFiles = 64,
        SerializationAssemblyFiles = 128,
        SuggestedRedirects = 256
    }
}
