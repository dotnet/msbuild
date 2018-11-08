namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Abstractions
{
    internal interface IResolveAssemblyReferenceTask
    {
        ResolveAssemblyReferenceTaskOutput Execute(ResolveAssemblyReferenceTaskInput input);
    }
}
