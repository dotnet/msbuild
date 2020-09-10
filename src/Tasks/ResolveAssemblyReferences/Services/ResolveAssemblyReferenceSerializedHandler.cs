// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Services
{
    internal sealed class ResolveAssemblyReferenceSerializedHandler : IResolveAssemblyReferenceTaskHandler
    {
        private const int MaxNumberOfConcurentClients = 1;

        private readonly IResolveAssemblyReferenceTaskHandler _taskHandler;
        private readonly AsyncSemaphore _semaphore;

        public ResolveAssemblyReferenceSerializedHandler(IResolveAssemblyReferenceTaskHandler taskHandler)
        {
            _taskHandler = taskHandler;
            _semaphore = new AsyncSemaphore(MaxNumberOfConcurentClients);
        }

        public ResolveAssemblyReferenceSerializedHandler() : this(new ResolveAssemblyReferenceHandler())
        {
        }

        public async Task<ResolveAssemblyReferenceResult> ExecuteAsync(ResolveAssemblyReferenceRequest input, CancellationToken cancellationToken = default)
        {
            using (await _semaphore.EnterAsync(cancellationToken))
            {
                NativeMethodsShared.SetCurrentDirectory(input.CurrentPath);
                ResolveAssemblyReferenceResult result = await _taskHandler.ExecuteAsync(input, cancellationToken);
                return result;
            }
        }

        public void Dispose()
        {
            _semaphore?.Dispose();
        }
    }
}
