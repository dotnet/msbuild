// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Tasks.AssemblyDependency;
using Microsoft.Build.Utilities;
using Shouldly;

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests
{
    [TestClass]
    public sealed class RarNodeExecuteRequest_Tests
    {
        [MSBuildTestMethod]
        public void TaskInputsArePropagated()
        {
            ResolveAssemblyReference clientRar = new()
            {
                BuildEngine = new MockEngine(),
                Assemblies = [new TaskItem("System"), new TaskItem("System.IO")],
                AssemblyFiles = [],
                AllowedAssemblyExtensions = [".dll", ".exe"],
                SearchPaths = ["{HintPathFromItem}", "{CandidateAssemblyFiles}"],
                AutoUnify = true,
                FindDependencies = true,
                Silent = true,
                TargetProcessorArchitecture = "AMD64",
            };
            RarNodeExecuteRequest request = new(clientRar);

            ResolveAssemblyReference nodeRar = new();
            request.SetTaskInputs(nodeRar, CreateBuildEngine());

            Assert.AreEqual(clientRar.Assemblies.Length, nodeRar.Assemblies.Length);
            for (int i = 0; i < clientRar.Assemblies.Length; i++)
            {
                Assert.AreEqual(clientRar.Assemblies[i].ItemSpec, nodeRar.Assemblies[i].ItemSpec);
            }

            Assert.AreEqual(clientRar.AssemblyFiles.Length, nodeRar.AssemblyFiles.Length);
            nodeRar.AllowedAssemblyExtensions.ShouldBe(clientRar.AllowedAssemblyExtensions);
            nodeRar.SearchPaths.ShouldBe(clientRar.SearchPaths);
            Assert.AreEqual(clientRar.AutoUnify, nodeRar.AutoUnify);
            Assert.AreEqual(clientRar.FindDependencies, nodeRar.FindDependencies);
            Assert.AreEqual(clientRar.Silent, nodeRar.Silent);
            Assert.AreEqual(clientRar.TargetProcessorArchitecture, nodeRar.TargetProcessorArchitecture, StringComparer.Ordinal);

            // Pick some unused inputs to ensure they remained at their defaults.
            nodeRar.AllowedRelatedFileExtensions.ShouldBe(clientRar.AllowedRelatedFileExtensions);
            Assert.AreEqual(clientRar.AppConfigFile, nodeRar.AppConfigFile);
            Assert.AreEqual(clientRar.IgnoreDefaultInstalledAssemblySubsetTables, nodeRar.IgnoreDefaultInstalledAssemblySubsetTables);
            Assert.AreEqual(clientRar.FindSatellites, nodeRar.FindSatellites);
            Assert.AreEqual(clientRar.StateFile, nodeRar.StateFile);
        }

        [MSBuildTestMethod]
        public void BuildEngineSettingsArePropagated()
        {
            MockEngine mockEngine = new()
            {
                MinimumMessageImportance = MessageImportance.Normal,
                SetIsTaskInputLoggingEnabled = false,
            };
            ResolveAssemblyReference clientRar = new() { BuildEngine = mockEngine };
            RarNodeExecuteRequest request = new(clientRar);

            ResolveAssemblyReference nodeRar = new();
            request.SetTaskInputs(nodeRar, CreateBuildEngine());

            Assert.AreEqual(mockEngine.LineNumberOfTaskNode, nodeRar.BuildEngine.LineNumberOfTaskNode);
            Assert.AreEqual(mockEngine.ColumnNumberOfTaskNode, nodeRar.BuildEngine.ColumnNumberOfTaskNode);
            Assert.AreEqual(mockEngine.ProjectFileOfTaskNode, nodeRar.BuildEngine.ProjectFileOfTaskNode);
            IBuildEngine10 buildEngine10 = Assert.IsInstanceOfType<IBuildEngine10>(nodeRar.BuildEngine);
            EngineServices engineServices = buildEngine10.EngineServices;
            Assert.AreEqual(mockEngine.LogsMessagesOfImportance(MessageImportance.Low), engineServices.LogsMessagesOfImportance(MessageImportance.Low));
            Assert.AreEqual(mockEngine.LogsMessagesOfImportance(MessageImportance.Normal), engineServices.LogsMessagesOfImportance(MessageImportance.Normal));
            Assert.AreEqual(mockEngine.LogsMessagesOfImportance(MessageImportance.High), engineServices.LogsMessagesOfImportance(MessageImportance.High));
            Assert.AreEqual(mockEngine.IsTaskInputLoggingEnabled, engineServices.IsTaskInputLoggingEnabled);
        }

        [MSBuildTestMethod]
        public void OutOfProcExecutionFlagsAreDisabledOnHydrate()
        {
            MockEngine mockEngine = new()
            {
                SetIsOutOfProcRarNodeEnabled = true,
            };
            ResolveAssemblyReference clientRar = new()
            {
                BuildEngine = mockEngine,
                AllowOutOfProcNode = true,
            };
            RarNodeExecuteRequest request = new(clientRar);

            ResolveAssemblyReference nodeRar = new();
            request.SetTaskInputs(nodeRar, CreateBuildEngine());

            IBuildEngine10 buildEngine10 = Assert.IsInstanceOfType<IBuildEngine10>(nodeRar.BuildEngine);
            EngineServices engineServices = buildEngine10.EngineServices;
            Assert.IsFalse(nodeRar.AllowOutOfProcNode);
            Assert.IsFalse(engineServices.IsOutOfProcRarNodeEnabled);
        }

        private RarNodeBuildEngine CreateBuildEngine()
        {
            // Since RarNodeBuildEngine normally handles buffering log events back to the client, we need to pass it a
            // pipe server. We don't ever connect to a client for these tests though, so we can immediately dispose it.
            OutOfProcRarNodeEndpoint.SharedConfig config = OutOfProcRarNodeEndpoint.CreateConfig(maxNumberOfServerInstances: 1);
            using NodePipeServer pipeServer = new(config.PipeName, config.Handshake, config.MaxNumberOfServerInstances);
            return new RarNodeBuildEngine(pipeServer);
        }
    }
}
