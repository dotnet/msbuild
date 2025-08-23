// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Tasks.AssemblyDependency;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests
{
    public sealed class RarNodeExecuteRequest_Tests
    {
        [Fact]
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
            request.SetTaskInputs(nodeRar, new RarNodeBuildEngine());

            Assert.Equal(clientRar.Assemblies.Length, nodeRar.Assemblies.Length);
            for (int i = 0; i < clientRar.Assemblies.Length; i++)
            {
                Assert.Equal(clientRar.Assemblies[i].ItemSpec, nodeRar.Assemblies[i].ItemSpec);
            }

            Assert.Equal(clientRar.AssemblyFiles.Length, nodeRar.AssemblyFiles.Length);
            Assert.Equal(clientRar.AllowedAssemblyExtensions, nodeRar.AllowedAssemblyExtensions, StringComparer.Ordinal);
            Assert.Equal(clientRar.SearchPaths, nodeRar.SearchPaths, StringComparer.Ordinal);
            Assert.Equal(clientRar.AutoUnify, nodeRar.AutoUnify);
            Assert.Equal(clientRar.FindDependencies, nodeRar.FindDependencies);
            Assert.Equal(clientRar.Silent, nodeRar.Silent);
            Assert.Equal(clientRar.TargetProcessorArchitecture, nodeRar.TargetProcessorArchitecture, StringComparer.Ordinal);

            // Pick some unused inputs to ensure they remained at their defaults.
            Assert.Equal(clientRar.AllowedRelatedFileExtensions, nodeRar.AllowedRelatedFileExtensions);
            Assert.Equal(clientRar.AppConfigFile, nodeRar.AppConfigFile);
            Assert.Equal(clientRar.IgnoreDefaultInstalledAssemblySubsetTables, nodeRar.IgnoreDefaultInstalledAssemblySubsetTables);
            Assert.Equal(clientRar.FindSatellites, nodeRar.FindSatellites);
            Assert.Equal(clientRar.StateFile, nodeRar.StateFile);
        }

        [Fact]
        public void KnownRelativePathsAreResolvedToFullPaths()
        {
            const string AppConfigFileName = "App.config";
            const string StateFileName = "AssemblyReference.cache";
            ResolveAssemblyReference clientRar = new()
            {
                BuildEngine = new MockEngine(),
                AppConfigFile = AppConfigFileName,
                StateFile = StateFileName,
            };
            RarNodeExecuteRequest request = new(clientRar);

            ResolveAssemblyReference nodeRar = new();
            request.SetTaskInputs(nodeRar, new RarNodeBuildEngine());

            Assert.Equal(Path.GetFullPath(AppConfigFileName), nodeRar.AppConfigFile);
            Assert.Equal(Path.GetFullPath(StateFileName), nodeRar.StateFile);
        }

        [Fact]
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
            request.SetTaskInputs(nodeRar, new RarNodeBuildEngine());

            Assert.Equal(mockEngine.LineNumberOfTaskNode, nodeRar.BuildEngine.LineNumberOfTaskNode);
            Assert.Equal(mockEngine.ColumnNumberOfTaskNode, nodeRar.BuildEngine.ColumnNumberOfTaskNode);
            Assert.Equal(mockEngine.ProjectFileOfTaskNode, nodeRar.BuildEngine.ProjectFileOfTaskNode);
            IBuildEngine10 buildEngine10 = Assert.IsAssignableFrom<IBuildEngine10>(nodeRar.BuildEngine);
            EngineServices engineServices = buildEngine10.EngineServices;
            Assert.Equal(mockEngine.LogsMessagesOfImportance(MessageImportance.Low), engineServices.LogsMessagesOfImportance(MessageImportance.Low));
            Assert.Equal(mockEngine.LogsMessagesOfImportance(MessageImportance.Normal), engineServices.LogsMessagesOfImportance(MessageImportance.Normal));
            Assert.Equal(mockEngine.LogsMessagesOfImportance(MessageImportance.High), engineServices.LogsMessagesOfImportance(MessageImportance.High));
            Assert.Equal(mockEngine.IsTaskInputLoggingEnabled, engineServices.IsTaskInputLoggingEnabled);
        }

        [Fact]
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
            request.SetTaskInputs(nodeRar, new RarNodeBuildEngine());

            IBuildEngine10 buildEngine10 = Assert.IsAssignableFrom<IBuildEngine10>(nodeRar.BuildEngine);
            EngineServices engineServices = buildEngine10.EngineServices;
            Assert.False(nodeRar.AllowOutOfProcNode);
            Assert.False(engineServices.IsOutOfProcRarNodeEnabled);
        }
    }
}
