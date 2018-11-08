using Microsoft.Build.Tasks.ResolveAssemblyReferences.Abstractions;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Engine
{
    internal class ResolveAssemblyReferenceStatelessTask : IResolveAssemblyReferenceTask
    {
        public ResolveAssemblyReferenceTaskOutput Execute(ResolveAssemblyReferenceTaskInput input)
        {
            var rarTask = new ResolveAssemblyReference { Input = input };
            rarTask.Execute();
            return rarTask.Output;
        }
    }
}
