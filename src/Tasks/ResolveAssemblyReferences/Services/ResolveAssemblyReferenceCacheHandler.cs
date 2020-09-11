// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Services
{
    internal sealed class ResolveAssemblyReferenceCacheHandler : IResolveAssemblyReferenceTaskHandler
    {
        private readonly struct CacheEntry
        {
            public CacheEntry(ResolveAssemblyReferenceRequest request, ResolveAssemblyReferenceResult result)
            {
                Request = request;
                Result = result;
            }

            public ResolveAssemblyReferenceRequest Request { get; }
            public ResolveAssemblyReferenceResult Result { get; }
        }

        private readonly object _lock = new object();

        private readonly Dictionary<string, CacheEntry> _cache;

        private readonly IResolveAssemblyReferenceTaskHandler _handler;

        public ResolveAssemblyReferenceCacheHandler(IResolveAssemblyReferenceTaskHandler handler)
        {
            _handler = handler;
            _cache = new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        }

        public async Task<ResolveAssemblyReferenceResult> ExecuteAsync(ResolveAssemblyReferenceRequest input, CancellationToken cancellationToken = default)
        {
            string projectId = input.StateFile;

            lock (_lock)
            {
                if (projectId != null && _cache.ContainsKey(projectId))
                {
                    Console.WriteLine($"Found entry for project: '{projectId}'");
                    CacheEntry entry = _cache[projectId];

                    if (ResolveAssemblyReferenceComparer.CompareInput(input, entry.Request))
                    {
                        return entry.Result;
                    }

                    // Not matching, remove it from cache
                    _cache.Remove(projectId);
                }
            }

            ResolveAssemblyReferenceResult result = await _handler.ExecuteAsync(input, cancellationToken);

            lock (_lock)
            {
                Console.WriteLine("Adding new entry to cache");
                if (projectId != null)
                    _cache[projectId] = new CacheEntry(input, result);
            }

            return result;
        }

        public void Dispose()
        {
            _handler.Dispose();
        }
    }
}
