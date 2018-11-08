using Microsoft.Build.Tasks.ResolveAssemblyReferences.Domain;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Abstractions
{
    internal interface IResolveAssemblyReferenceService
    {
        ResolveAssemblyReferenceResponse ResolveAssemblyReferences(ResolveAssemblyReferenceRequest req);
    }
}
