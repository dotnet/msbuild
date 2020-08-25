using System.Threading;
using System.Threading.Tasks;

using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Services
{
    internal sealed class ResolveAssemblyReferenceTaskHandler : IResolveAssemblyReferenceTaskHandler
    {
        public Task<ResolveAssemblyReferenceResult> ExecuteAsync(ResolveAssemblyReferenceRequest input, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Execute(input));

        }

        internal ResolveAssemblyReferenceResult Execute(ResolveAssemblyReferenceRequest input)
        {
            ResolveAssemblyReferenceTaskInput taskInput = new ResolveAssemblyReferenceTaskInput(input);
            ResolveAssemblyReferenceBuildEngine buildEngine = new ResolveAssemblyReferenceBuildEngine();
            ResolveAssemblyReference task = new ResolveAssemblyReference()
            {
                BuildEngine = buildEngine
            };

            ResolveAssemblyReferenceResult result = task.Execute(taskInput);
            result.BuildEventArgs = buildEngine.BuildEvent;
            return result;
        }

        public void Dispose()
        {
        }
    }
}
