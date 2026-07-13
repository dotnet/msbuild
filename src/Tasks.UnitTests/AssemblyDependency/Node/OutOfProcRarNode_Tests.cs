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

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests
{
    /// <summary>
    /// End-to-end tests for RAR out-of-proc execution.
    /// The actual inputs for RAR should be kept simple since we're not aiming to test the full serialization format
    /// or RAR logic itself here.
    /// </summary>
    [TestClass]
    public sealed class OutOfProcRarNode_Tests(TestContext output)
    {
        [MSBuildTestMethod]
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
            Assert.IsNotNull(rarClient);
            Assert.IsTrue(result);
            Assert.AreEqual(0, engine.Warnings);
            Assert.AreEqual(0, engine.Errors);
            _ = Assert.ContainsSingle(rar.ResolvedFiles);

            rarClient.Dispose();
            cts.Cancel();
            runTask.GetAwaiter().GetResult();
        }

        [MSBuildTestMethod]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(false, false)]
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
            Assert.IsNull(rarClient);
            Assert.IsTrue(result);
            Assert.AreEqual(0, engine.Warnings);
            Assert.AreEqual(0, engine.Errors);
            _ = Assert.ContainsSingle(rar.ResolvedFiles);
        }

        [MSBuildTestMethod]
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
            Assert.IsNotNull(rarClient);
            Assert.IsTrue(result);
            Assert.AreEqual(0, engine.Warnings);
            Assert.AreEqual(0, engine.Errors);
            _ = Assert.ContainsSingle(rar.ResolvedFiles);
        }
    }
}
