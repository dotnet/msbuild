// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Services
{
    internal sealed class ResolveAssemblyReferenceHandler : IResolveAssemblyReferenceTaskHandler
    {
        public Task<ResolveAssemblyReferenceResult> ExecuteAsync(ResolveAssemblyReferenceRequest input, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Execute(input), cancellationToken);
        }

        internal ResolveAssemblyReferenceResult Execute(ResolveAssemblyReferenceRequest input)
        {
            ResolveAssemblyReferenceTaskInput taskInput = new ResolveAssemblyReferenceTaskInput(input);
            ResolveAssemblyReferenceBuildEngine buildEngine = new ResolveAssemblyReferenceBuildEngine();
            ResolveAssemblyReference task = new ResolveAssemblyReference
            {
                BuildEngine = buildEngine
            };

            ResolveAssemblyReferenceResult result = task.Execute(taskInput);
            result.BuildEvents = buildEngine.BuildEvents;

            return result;
        }

        public void Dispose()
        {
        }
    }
}
