// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
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
            ResolveAssemblyReference rar = new()
            {
                AllowOutOfProcNode = true,
                BuildEngine = engine,
                Assemblies = [new TaskItem("System")],
                SearchPaths = [Path.GetDirectoryName(typeof(object).Module.FullyQualifiedName)],
            };

            using OutOfProcRarNodeEndpoint endpoint = new(
                    endpointId: 0,
                    OutOfProcRarNodeEndpoint.CreateConfig(maxNumberOfServerInstances: 1));
            using CancellationTokenSource cts = new();
            System.Threading.Tasks.Task runTask = endpoint.RunAsync(cts.Token);

            bool result = rar.Execute();

            // If the out-of-proc path was executed, a client should be registered.
            using OutOfProcRarClient? rarClient = engine.GetRegisteredTaskObject(OutOfProcRarClient.TaskObjectCacheKey, RegisteredTaskObjectLifetime.Build) as OutOfProcRarClient;
            Assert.NotNull(rarClient);
            Assert.True(result);
            Assert.Equal(0, engine.Warnings);
            Assert.Equal(0, engine.Errors);
            _ = Assert.Single(rar.ResolvedFiles);

            rarClient.Dispose();
            cts.Cancel();
            runTask.GetAwaiter().GetResult();
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
