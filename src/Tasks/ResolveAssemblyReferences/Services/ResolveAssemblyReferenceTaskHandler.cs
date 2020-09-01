using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Server;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Services
{
    internal sealed class ResolveAssemblyReferenceTaskHandler : IResolveAssemblyReferenceTaskHandler
    {
        private ResolveAssemblyReferenceTaskOutput EmptyOutput => new ResolveAssemblyReference().ResolveAssemblyReferenceOutput;

        private readonly ResolveAssemblyReference _task = new ResolveAssemblyReference();

        private ResolveAssemblyReference GetResolveAssemblyReferenceTask(IBuildEngine buildEngine)
        {
            _task.BuildEngine = buildEngine;
            _task.ResolveAssemblyReferenceOutput = EmptyOutput;

            return _task;
        }

        public Task<ResolveAssemblyReferenceResult> ExecuteAsync(ResolveAssemblyReferenceRequest input, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Execute(input));

        }

        internal ResolveAssemblyReferenceResult Execute(ResolveAssemblyReferenceRequest input)
        {
            ResolveAssemblyReferenceTaskInput taskInput = new ResolveAssemblyReferenceTaskInput(input);
            ResolveAssemblyReferenceBuildEngine buildEngine = new ResolveAssemblyReferenceBuildEngine();
            //ResolveAssemblyReference task = GetResolveAssemblyReferenceTask(buildEngine);
            ResolveAssemblyReference task = new ResolveAssemblyReference
            {
                BuildEngine = buildEngine
            };

            ResolveAssemblyReferenceResult result = task.Execute(taskInput);
            //result.CustomBuildEvents = buildEngine.CustomBuildEvent;
            //result.BuildMessageEvents = buildEngine.MessageBuildEvent;
            //result.BuildWarningEvents = buildEngine.WarningBuildEvent;
            //result.BuildErrorEvents = buildEngine.ErrorBuildEvent;

            //result.EventCount = buildEngine.EventCount;

            //System.Console.WriteLine("RAR task: {0}. Logged {1} events", result.TaskResult ? "Succeded" : "Failed", result.EventCount);

            return result;
        }

        public void Dispose()
        {
        }
    }

    internal sealed class ResolveAssemlyReferenceCacheHandler : IResolveAssemblyReferenceTaskHandler
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

        private static int RequestNum = 0;

        public ResolveAssemlyReferenceCacheHandler(IResolveAssemblyReferenceTaskHandler handler)
        {
            _handler = handler;
            _cache = new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        }

        public async Task<ResolveAssemblyReferenceResult> ExecuteAsync(ResolveAssemblyReferenceRequest input, CancellationToken cancellationToken = default)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            string projectId = input.StateFile;

            int requestId = Interlocked.Increment(ref RequestNum);

            lock (_lock)
            {
                if (_cache.ContainsKey(projectId))
                {
                    Console.WriteLine($"Found entry for project: '{projectId}'");
                    CacheEntry entry = _cache[projectId];

                    if (ResolveAssemblyReferenceComparer.CompareInput(input, entry.Request))
                    {
                        PrintDiagnostic(requestId, stopwatch, true);
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
                _cache[projectId] = new CacheEntry(input, result);
            }

            PrintDiagnostic(requestId, stopwatch, false);
            return result;
        }

        private static void PrintDiagnostic(int requestId, Stopwatch stopwatch, bool cache)
        {
            stopwatch.Stop();
            Console.WriteLine("{0}; Cached used: {1}; Elapsed: {2} ms", requestId, cache, stopwatch.ElapsedMilliseconds);
        }

        public void Dispose()
        {
            _handler.Dispose();
        }
    }
}
