// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Tasks.AssemblyDependency;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests
{
    /// <summary>
    /// End-to-end tests for RAR out-of-proc execution.
    /// The actual inputs for RAR should be kept simple since we're not aiming to test the full serialization format
    /// or RAR logic itself here.
    /// </summary>
    public sealed class OutOfProcRarNode_Tests(ITestOutputHelper output)
    {
        [Fact]
        public void RunsOutOfProcIfAllFlagsAreEnabled()
        {
            MockEngine engine = new(output)
            {
                SetIsOutOfProcRarNodeEnabled = true,
            };
            string searchPath = Path.GetDirectoryName(typeof(object).Module.FullyQualifiedName);
            ResolveAssemblyReference rar = new()
            {
                AllowOutOfProcNode = true,
                BuildEngine = engine,
                Assemblies = [new TaskItem("System")],
                SearchPaths = [searchPath],
            };

            // Emit diagnostic information for troubleshooting intermittent failures
            output.WriteLine($"=== Test Diagnostics ===");
            output.WriteLine($"OS: {RuntimeInformation.OSDescription}");
            output.WriteLine($"Framework: {RuntimeInformation.FrameworkDescription}");
            output.WriteLine($"Process ID: {Environment.ProcessId}");
            output.WriteLine($"Thread ID: {Environment.CurrentManagedThreadId}");
            output.WriteLine($"Search path exists: {Directory.Exists(searchPath)}");
            output.WriteLine($"Engine.SetIsOutOfProcRarNodeEnabled: {engine.SetIsOutOfProcRarNodeEnabled}");
            output.WriteLine($"RAR.AllowOutOfProcNode: {rar.AllowOutOfProcNode}");
            output.WriteLine($"======================");

            using OutOfProcRarNodeEndpoint endpoint = new(
                    endpointId: 0,
                    OutOfProcRarNodeEndpoint.CreateConfig(maxNumberOfServerInstances: 1));
            using CancellationTokenSource cts = new();
            System.Threading.Tasks.Task runTask = endpoint.RunAsync(cts.Token);

            // Wait for endpoint server to be ready before calling Execute.
            // Introduce a short delay to reduce race between endpoint startup and connection attempt.
            // This improves reliability without changing test semantics.
            const int startupDelayMs = 200;
            output.WriteLine($"Waiting {startupDelayMs}ms for endpoint startup...");
            System.Threading.Tasks.Task.Delay(startupDelayMs).Wait();

            Stopwatch sw = Stopwatch.StartNew();
            bool result = rar.Execute();
            sw.Stop();
            output.WriteLine($"rar.Execute() completed in {sw.ElapsedMilliseconds}ms, result={result}");

            // If the out-of-proc path was executed, a client should be registered.
            using OutOfProcRarClient? rarClient = engine.GetRegisteredTaskObject(OutOfProcRarClient.TaskObjectCacheKey, RegisteredTaskObjectLifetime.Build) as OutOfProcRarClient;

            // Dump diagnostics on failure
            if (!result || rarClient == null)
            {
                DumpDiagnostics(output, engine, result, rarClient);
            }
            else
            {
                // On success, log correlation information
                output.WriteLine($"OutOfProcRarClient registered successfully");
                if (rar.ResolvedFiles != null && rar.ResolvedFiles.Length > 0)
                {
                    output.WriteLine($"Resolved file: {rar.ResolvedFiles[0].ItemSpec}");
                }
            }

            Assert.NotNull(rarClient);
            Assert.True(result);
            Assert.Equal(0, engine.Warnings);
            Assert.Equal(0, engine.Errors);
            _ = Assert.Single(rar.ResolvedFiles);

            rarClient.Dispose();
            cts.Cancel();
            runTask.GetAwaiter().GetResult();
        }

        /// <summary>
        /// Dump diagnostic information when test fails to help troubleshoot intermittent issues.
        /// </summary>
        private void DumpDiagnostics(ITestOutputHelper output, MockEngine engine, bool result, OutOfProcRarClient? rarClient)
        {
            output.WriteLine("=== FAILURE DIAGNOSTICS ===");
            output.WriteLine($"rar.Execute() result: {result}");
            output.WriteLine($"rarClient is null: {rarClient == null}");
            output.WriteLine($"Engine.Warnings: {engine.Warnings}");
            output.WriteLine($"Engine.Errors: {engine.Errors}");
            output.WriteLine("");
            output.WriteLine("=== MockEngine Log ===");
            output.WriteLine(engine.Log);
            output.WriteLine("======================");
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(false, false)]
        public void SkipsOutOfProcNodeIfAnyFlagIsDisabled(bool buildEngineFlag, bool taskInputFlag)
        {
            MockEngine engine = new(output)
            {
                SetIsOutOfProcRarNodeEnabled = buildEngineFlag,
            };
            ResolveAssemblyReference rar = new()
            {
                AllowOutOfProcNode = taskInputFlag,
                BuildEngine = engine,
                Assemblies = [new TaskItem("System")],
                SearchPaths = [Path.GetDirectoryName(typeof(object).Module.FullyQualifiedName)],
            };

            bool result = rar.Execute();

            // If the out-of-proc path was skipped, no client should be registered.
            using OutOfProcRarClient? rarClient = engine.GetRegisteredTaskObject(OutOfProcRarClient.TaskObjectCacheKey, RegisteredTaskObjectLifetime.Build) as OutOfProcRarClient;
            Assert.Null(rarClient);
            Assert.True(result);
            Assert.Equal(0, engine.Warnings);
            Assert.Equal(0, engine.Errors);
            _ = Assert.Single(rar.ResolvedFiles);
        }

        [Fact]
        public void FallsBackToInProcTaskIfClientFailsToConnect()
        {
            MockEngine engine = new(output)
            {
                SetIsOutOfProcRarNodeEnabled = true,
            };
            ResolveAssemblyReference rar = new()
            {
                AllowOutOfProcNode = true,
                BuildEngine = engine,
                Assemblies = [new TaskItem("System")],
                SearchPaths = [Path.GetDirectoryName(typeof(object).Module.FullyQualifiedName)],
            };

            bool result = rar.Execute();

            // If the out-of-proc path was attempted but failed, a client should be registered.
            using OutOfProcRarClient? rarClient = engine.GetRegisteredTaskObject(OutOfProcRarClient.TaskObjectCacheKey, RegisteredTaskObjectLifetime.Build) as OutOfProcRarClient;
            Assert.NotNull(rarClient);
            Assert.True(result);
            Assert.Equal(0, engine.Warnings);
            Assert.Equal(0, engine.Errors);
            _ = Assert.Single(rar.ResolvedFiles);
        }
    }
}
