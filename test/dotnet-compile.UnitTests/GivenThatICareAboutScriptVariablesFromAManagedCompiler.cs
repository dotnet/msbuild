// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.ProjectModel;
using Moq;
using NuGet.Frameworks;
using Xunit;
using Microsoft.DotNet.Cli.Utils;
using FluentAssertions;
using System.Linq;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.DotNet.Tools.Compiler.Tests
{
    public class GivenThatICareAboutScriptVariablesFromAManagedCompiler : IClassFixture<ScriptVariablesFixture>
    {
        private readonly ScriptVariablesFixture _fixture;

        public GivenThatICareAboutScriptVariablesFromAManagedCompiler(ScriptVariablesFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void It_passes_a_FullTargetFramework_variable_to_the_pre_compile_scripts()
        {
            _fixture.PreCompileScriptVariables.Should().ContainKey("compile:FullTargetFramework");
            _fixture.PreCompileScriptVariables["compile:FullTargetFramework"].Should().Be("dnxcore,Version=v5.0");
        }

        [Fact]
        public void It_passes_a_TargetFramework_variable_to_the_pre_compile_scripts()
        {
            _fixture.PreCompileScriptVariables.Should().ContainKey("compile:TargetFramework");
            _fixture.PreCompileScriptVariables["compile:TargetFramework"].Should().Be("dnxcore50");
        }

        [Fact]
        public void It_passes_a_Configuration_variable_to_the_pre_compile_scripts()
        {
            _fixture.PreCompileScriptVariables.Should().ContainKey("compile:Configuration");
            _fixture.PreCompileScriptVariables["compile:Configuration"].Should().Be(
                ScriptVariablesFixture.ConfigValue);
        }

        [Fact]
        public void It_passes_a_OutputFile_variable_to_the_pre_compile_scripts()
        {
            _fixture.PreCompileScriptVariables.Should().ContainKey("compile:OutputFile");
            _fixture.PreCompileScriptVariables["compile:OutputFile"].Should().Be(ScriptVariablesFixture.OutputFile);
        }

        [Fact]
        public void It_passes_a_OutputDir_variable_to_the_pre_compile_scripts()
        {
            _fixture.PreCompileScriptVariables.Should().ContainKey("compile:OutputDir");
            _fixture.PreCompileScriptVariables["compile:OutputDir"].Should().Be(ScriptVariablesFixture.OutputPath);
        }

        [Fact]
        public void It_passes_a_ResponseFile_variable_to_the_pre_compile_scripts()
        {
            _fixture.PreCompileScriptVariables.Should().ContainKey("compile:ResponseFile");
            _fixture.PreCompileScriptVariables["compile:ResponseFile"].Should().Be(ScriptVariablesFixture.ResponseFile);
        }

        [Fact]
        public void It_passes_a_RuntimeOutputDir_variable_to_the_pre_compile_scripts_if_rid_is_set_in_the_ProjectContext()
        {
            var rid = PlatformServices.Default.Runtime.GetLegacyRestoreRuntimeIdentifier();
            var fixture = ScriptVariablesFixture.GetFixtureWithRids(rid);
            fixture.PreCompileScriptVariables.Should().ContainKey("compile:RuntimeOutputDir");
            fixture.PreCompileScriptVariables["compile:RuntimeOutputDir"].Should().Be(fixture.RuntimeOutputDir);
        }

        [Fact]
        public void It_passes_a_FullTargetFramework_variable_to_the_post_compile_scripts()
        {
            _fixture.PostCompileScriptVariables.Should().ContainKey("compile:FullTargetFramework");
            _fixture.PostCompileScriptVariables["compile:FullTargetFramework"].Should().Be("dnxcore,Version=v5.0");
        }

        [Fact]
        public void It_passes_a_TargetFramework_variable_to_the_post_compile_scripts()
        {
            _fixture.PostCompileScriptVariables.Should().ContainKey("compile:TargetFramework");
            _fixture.PostCompileScriptVariables["compile:TargetFramework"].Should().Be("dnxcore50");
        }

        [Fact]
        public void It_passes_a_Configuration_variable_to_the_post_compile_scripts()
        {
            _fixture.PostCompileScriptVariables.Should().ContainKey("compile:Configuration");
            _fixture.PostCompileScriptVariables["compile:Configuration"].Should().Be(
                ScriptVariablesFixture.ConfigValue);
        }

        [Fact]
        public void It_passes_a_OutputFile_variable_to_the_post_compile_scripts()
        {
            _fixture.PostCompileScriptVariables.Should().ContainKey("compile:OutputFile");
            _fixture.PostCompileScriptVariables["compile:OutputFile"].Should().Be(ScriptVariablesFixture.OutputFile);
        }

        [Fact]
        public void It_passes_a_OutputDir_variable_to_the_post_compile_scripts()
        {
            _fixture.PostCompileScriptVariables.Should().ContainKey("compile:OutputDir");
            _fixture.PostCompileScriptVariables["compile:OutputDir"].Should().Be(ScriptVariablesFixture.OutputPath);
        }

        [Fact]
        public void It_passes_a_ResponseFile_variable_to_the_post_compile_scripts()
        {
            _fixture.PostCompileScriptVariables.Should().ContainKey("compile:ResponseFile");
            _fixture.PostCompileScriptVariables["compile:ResponseFile"].Should().Be(ScriptVariablesFixture.ResponseFile);
        }

        [Fact]
        public void It_passes_a_CompilerExitCode_variable_to_the_post_compile_scripts()
        {
            _fixture.PostCompileScriptVariables.Should().ContainKey("compile:CompilerExitCode");
            _fixture.PostCompileScriptVariables["compile:CompilerExitCode"].Should().Be("0");
        }

        [Fact]
        public void It_passes_a_RuntimeOutputDir_variable_to_the_post_compile_scripts_if_rid_is_set_in_the_ProjectContext()
        {
            var rid = PlatformServices.Default.Runtime.GetLegacyRestoreRuntimeIdentifier();
            var fixture = ScriptVariablesFixture.GetFixtureWithRids(rid);
            fixture.PostCompileScriptVariables.Should().ContainKey("compile:RuntimeOutputDir");
            fixture.PostCompileScriptVariables["compile:RuntimeOutputDir"].Should().Be(fixture.RuntimeOutputDir);
        }
    }

    public class ScriptVariablesFixture
    {
        public const string ConfigValue = "Debug";

        public static string TestAssetPath = Path.Combine(
            AppContext.BaseDirectory,
            "TestAssets",
            "TestProjects",
            "TestAppWithLibrary",
            "TestApp");

        public static string OutputPath = Path.Combine(
            TestAssetPath,
            "bin",
            ConfigValue,
            "dnxcore50");

        public string RuntimeOutputDir { get; private set; }

        public static string OutputFile = Path.Combine(OutputPath, "TestApp.dll");

        public static string ResponseFile = Path.Combine(
            TestAssetPath,
            "obj",
            ConfigValue,
            "dnxcore50",
            "dotnet-compile.rsp");

        public Dictionary<string, string> PreCompileScriptVariables { get; private set; }
        public Dictionary<string, string> PostCompileScriptVariables { get; private set; }

        public ScriptVariablesFixture() : this(string.Empty)
        {
        }

        private ScriptVariablesFixture(string rid)
        {
            var projectJson = Path.Combine(TestAssetPath, "project.json");
            var command = new Mock<ICommand>();
            command.Setup(c => c.Execute()).Returns(new CommandResult());
            command.Setup(c => c.OnErrorLine(It.IsAny<Action<string>>())).Returns(() => command.Object);
            command.Setup(c => c.OnOutputLine(It.IsAny<Action<string>>())).Returns(() => command.Object);
            var commandFactory = new Mock<ICommandFactory>();
            commandFactory.Setup(c => c
                .Create(
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<NuGetFramework>(),
                    It.IsAny<string>()))
                .Returns(command.Object);

            var _args = new CompilerCommandApp("dotnet compile", ".NET Compiler", "Compiler for the .NET Platform");
            _args.ConfigValue = ConfigValue;

            PreCompileScriptVariables = new Dictionary<string, string>();
            PostCompileScriptVariables = new Dictionary<string, string>();

            var _scriptRunner = new Mock<IScriptRunner>();
            _scriptRunner.Setup(
                s =>
                    s.RunScripts(It.IsAny<ProjectContext>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
                    .Callback<ProjectContext, string, Dictionary<string, string>>((p, n, v) =>
                    {
                        if (n.Equals(ScriptNames.PreCompile))
                        {
                            PreCompileScriptVariables = v;
                        }

                        if (n.Equals(ScriptNames.PostCompile))
                        {
                            PostCompileScriptVariables = v;
                        }
                    });

            var managedCompiler = new ManagedCompiler(_scriptRunner.Object, commandFactory.Object);

            var rids = new List<string>();
            if (!string.IsNullOrEmpty(rid))
            {
                rids.Add(rid);
            }

            var context = ProjectContext.Create(projectJson, new NuGetFramework("dnxcore", new Version(5, 0)), rids);
            managedCompiler.Compile(context, _args);

            RuntimeOutputDir = Path.Combine(OutputPath, rid);
        }

        public static ScriptVariablesFixture GetFixtureWithRids(string rid)
        {
            return new ScriptVariablesFixture(rid);
        }
    }
}
