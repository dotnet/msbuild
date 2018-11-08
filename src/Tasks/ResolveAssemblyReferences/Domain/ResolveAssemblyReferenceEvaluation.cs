namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Domain
{
    internal struct ResolveAssemblyReferenceEvaluation
    {
        internal ResolveAssemblyReferenceRequest Request { get; }

        internal ResolveAssemblyReferenceResponse Response { get; }

        internal ResolveAssemblyReferenceEvaluation
        (
            ResolveAssemblyReferenceRequest request,
            ResolveAssemblyReferenceResponse response
        )
        {
            Request = request;
            Response = response;
        } 
    }
}
