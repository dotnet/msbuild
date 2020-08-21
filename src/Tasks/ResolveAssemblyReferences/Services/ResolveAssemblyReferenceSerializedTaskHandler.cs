// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Services
{
    internal sealed class ResolveAssemblyReferenceSerializedTaskHandler : IResolveAssemblyReferenceTaskHandler
    {
        private const int MaxNumberOfConcurentClients = 1;

        private readonly IResolveAssemblyReferenceTaskHandler _taskHandler;
        private readonly AsyncSemaphore _semaphore;

        public ResolveAssemblyReferenceSerializedTaskHandler(IResolveAssemblyReferenceTaskHandler taskHandler)
        {
            _taskHandler = taskHandler;
            _semaphore = new AsyncSemaphore(MaxNumberOfConcurentClients);
        }

        public ResolveAssemblyReferenceSerializedTaskHandler() : this(new ResolveAssemblyReferenceTaskHandler())
        {
        }

        public async Task<ResolveAssemblyReferenceResult> ExecuteAsync(ResolveAssemblyReferenceRequest input, CancellationToken cancellationToken = default)
        {
            using (await _semaphore.EnterAsync(cancellationToken))
            {
                return await _taskHandler.ExecuteAsync(input, cancellationToken);
            }
        }

        public void Dispose()
        {
            _semaphore?.Dispose();
        }
    }
}
