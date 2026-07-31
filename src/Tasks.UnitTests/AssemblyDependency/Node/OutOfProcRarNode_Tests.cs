// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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
using Shouldly;
using Xunit;

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

        [Fact]
        public void DispatchesStructuredConflictEvents()
        {
            MockEngine engine = new(output);
            ResolveAssemblyReference rar = new()
            {
                BuildEngine = engine,
            };
            var victor = new AssemblyConflictReferenceDetails(
                "D, Version=1.0.0.0",
                "/libs/v1/D.dll",
                useUnifiedHeader: false,
                isPrimary: true,
                isResolved: true,
                unresolvedPrimaryItemSpec: null,
                [new AssemblyConflictDependee("/libs/v1/D.dll", ["D"])]);
            var victim = new AssemblyConflictReferenceDetails(
                "D, Version=2.0.0.0",
                "/libs/v2/D.dll",
                useUnifiedHeader: true,
                isPrimary: false,
                isResolved: true,
                unresolvedPrimaryItemSpec: null,
                [new AssemblyConflictDependee("/libs/B.dll", ["B"])]);
            var formats = new AssemblyConflictMessageFormats(
                "There was a conflict between \"{0}\" and \"{1}\".",
                "Choosing \"{0}\" because it has a higher version.",
                "\"{0}\" was chosen because it was primary and \"{1}\" was not.",
                "MSB3243: No way to resolve conflict between \"{0}\" and \"{1}\". Choosing \"{0}\" arbitrarily.",
                "References which depend on \"{0}\" [{1}].",
                "References which depend on or have been unified to \"{0}\" [{1}].",
                "Unresolved primary reference with an item include of \"{0}\".",
                "Project file item includes which caused reference \"{0}\".",
                "Found conflicts between different versions of \"{0}\" that could not be resolved.\n{1}");
            var detailsEvent = new AssemblyConflictDependencyDetailsMessageEventArgs(
                victor,
                victim,
                formats,
                "ResolveAssemblyReference",
                MessageImportance.Low,
                DateTime.UtcNow);
            var warningEvent = new AssemblyConflictWarningEventArgs(
                "D",
                victor.FusionName,
                victim.FusionName,
                AssemblyConflictLossReason.WasNotPrimary,
                victor,
                victim,
                formats,
                "MSB3277",
                "project.proj",
                1,
                2,
                "MSBuild.ResolveAssemblyReference.FoundConflicts",
                "ResolveAssemblyReference",
                DateTime.UtcNow);

            OutOfProcRarClient.DispatchBuildEvent(
                rar,
                LoggingEventType.AssemblyConflictDependencyDetailsEvent,
                detailsEvent);
            OutOfProcRarClient.DispatchBuildEvent(
                rar,
                LoggingEventType.AssemblyConflictWarningEvent,
                warningEvent);

            engine.MessageEvents.ShouldHaveSingleItem().ShouldBeOfType<AssemblyConflictDependencyDetailsMessageEventArgs>();
            engine.WarningEvents.ShouldHaveSingleItem().ShouldBeOfType<AssemblyConflictWarningEventArgs>();
        }
    }
}
