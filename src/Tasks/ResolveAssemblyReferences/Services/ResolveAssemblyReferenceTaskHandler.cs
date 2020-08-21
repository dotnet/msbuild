using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Services
{
    internal sealed class ResolveAssemblyReferenceTaskHandler : IResolveAssemblyReferenceTaskHandler
    {
        public Task<ResolveAssemblyReferenceResult> ExecuteAsync(ResolveAssemblyReferenceRequest input, CancellationToken cancellationToken = default)
        {
            ResolveAssemblyReferenceTaskInput taskInput = new ResolveAssemblyReferenceTaskInput(input);
            ResolveAssemblyReferenceBuildEngine buildEngine = new ResolveAssemblyReferenceBuildEngine();
            ResolveAssemblyReference task = new ResolveAssemblyReference()
            {
                BuildEngine = buildEngine
            };

            ResolveAssemblyReferenceResult result = task.Execute(taskInput);
            result.BuildEventArgs = buildEngine.BuildEvent;

            return Task.FromResult(result);
        }

        public void Dispose()
        {
        }
    }
}
