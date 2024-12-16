// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;

namespace Microsoft.Build.Tasks.AssemblyDependency
{
    public class ResolveAssemblyReferenceService : IDisposable
    {
        private readonly ResolveAssemblyReferenceServiceWorker[] _workers;

        public ResolveAssemblyReferenceService()
            : this(Environment.ProcessorCount)
        {
        }

        public ResolveAssemblyReferenceService(int degreeOfParallelism)
        {
            ConcurrentDictionary<string, byte> seenStateFiles = new(StringComparer.OrdinalIgnoreCase);
            RarExecutionCache evaluationCache = new(degreeOfParallelism);
            _workers = new ResolveAssemblyReferenceServiceWorker[degreeOfParallelism];

            for (int i = 0; i < _workers.Length; i++)
            {
                ResolveAssemblyReferenceServiceWorker worker = new(
                    workerId: i.ToString(),
                    degreeOfParallelism,
                    evaluationCache,
                    seenStateFiles);
                _workers[i] = worker;
            }
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            if (IsServerRunning())
            {
                return;
            }

            Console.WriteLine($"Service started.");

            Task[] serverTasks = new Task[_workers.Length];

            for (int i = 0; i < _workers.Length; i++)
            {
                // Force new Tasks to avoid delaying setup if a client immediately connects.
                // Avoid referencing indexer in the closure.
                ResolveAssemblyReferenceServiceWorker worker = _workers[i];
                serverTasks[i] = Task.Run(
                    () => worker.RunServerAsync(cancellationToken), cancellationToken);
            }

            try
            {
                await Task.WhenAll(serverTasks);
            }
            catch (OperationCanceledException)
            {
                // Could land here if Task.Run() itself was cancelled and the worker never started.
            }

            Console.WriteLine($"All workers successfully stopped. Exiting.");
        }

        private bool IsServerRunning()
        {
            return false;
            // string serverRunningMutexName = $@"Global\msbuild-rar-server-running-{_handshake.ComputeHash()}";

            // // First, check if the server has created the mutex.
            // // Use a mutex to avoid using a timeout or checking a max pipe instance exception.
            // bool isRunning = Mutex.TryOpenExisting(serverRunningMutexName, out Mutex? mutex);
            // mutex?.Dispose();

            // return isRunning;
        }

        public void Dispose()
        {
            foreach (ResolveAssemblyReferenceServiceWorker worker in _workers)
            {
                worker.Dispose();
            }
        }
    }
}
