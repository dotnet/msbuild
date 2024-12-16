// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.Tasks.AssemblyDependency
{
    internal class RarExecutionCache
    {
        private ConcurrentDictionary<ulong, RarExecutionResponse> _evaluationCache { get; } = [];

        private readonly SemaphoreSlim _ioSemaphore;

        internal RarExecutionCache(int ioParallelism)
        {
            _ioSemaphore = new SemaphoreSlim(ioParallelism);
        }

        public async Task<RarExecutionResponse?> GetCachedEvaluation(RarExecutionRequest request)
        {
            if (request.ByteHash == 0 || !_evaluationCache.TryGetValue(request.ByteHash, out RarExecutionResponse? cachedEvaluation))
            {
                return null;
            }

            return await IsCacheUpToDate(cachedEvaluation) ? cachedEvaluation : null;
        }

        private async Task<bool> IsCacheUpToDate(RarExecutionResponse cachedEvaluation)
        {
            SystemState cache = cachedEvaluation.Cache!;
            List<Task<bool>> workerTasks = new(cache.instanceLocalFileStateCache.Count);

            foreach (KeyValuePair<string, SystemState.FileState> kvp in cache.instanceLocalFileStateCache)
            {
                string filePath = kvp.Key;
                DateTime cachedLastWriteTimeUtc = kvp.Value.LastModified;

                workerTasks.Add(Task.Run(async () =>
                {
                    await _ioSemaphore.WaitAsync();

                    try
                    {
                        bool result = NativeMethodsShared.GetLastWriteFileUtcTime(filePath) == cachedLastWriteTimeUtc;

                        if (!result)
                        {
                            Console.WriteLine($"File:'{filePath}',Expected:'{cachedLastWriteTimeUtc}'");
                        }

                        return result;
                    }
                    finally
                    {
                        _ioSemaphore.Release();
                    }
                }));
            }

            foreach (KeyValuePair<string, bool> kvp in cache.instanceLocalDirectoryExists)
            {
                string directoryPath = kvp.Key;
                bool cachedDirectoryExists = kvp.Value;

                workerTasks.Add(Task.Run(async () =>
                {
                    await _ioSemaphore.WaitAsync();

                    try
                    {
                        bool result = FileUtilities.DirectoryExistsNoThrow(directoryPath) == cachedDirectoryExists;

                        if (!result)
                        {
                            Console.WriteLine($"Directory:'{directoryPath}',Expected:'{cachedDirectoryExists}'");
                        }

                        return result;
                    }
                    finally
                    {
                        _ioSemaphore.Release();
                    }
                }));
            }

            foreach (KeyValuePair<string, string[]> kvp in cache.instanceLocalDirectories)
            {
                string directoryPath = kvp.Key;
                string[] cachedDirectoryEnumeration = kvp.Value;

                workerTasks.Add(Task.Run(async () =>
                {
                    await _ioSemaphore.WaitAsync();

                    try
                    {
                        bool result = FileSystems.Default.EnumerateDirectories(directoryPath)
                            .SequenceEqual(cachedDirectoryEnumeration);

                        if (!result)
                        {
                            Console.WriteLine($"Enumeration:'{directoryPath}',Expected:'{cachedDirectoryEnumeration}'");
                        }

                        return result;
                    }
                    finally
                    {
                        _ioSemaphore.Release();
                    }
                }));
            }

            await Task.WhenAll([.. workerTasks]);

            return workerTasks.All(task => task.Result);
        }

        public void CacheEvaluation(RarExecutionRequest request, RarExecutionResponse response)
        {
            if (request.ByteHash == 0)
            {
                return;
            }

            _evaluationCache[request.ByteHash] = response;
        }
    }
}