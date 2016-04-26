// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.PlatformAbstractions;
using System.Runtime.InteropServices;
using Xunit;
using FluentAssertions;

namespace Microsoft.DotNet.Tests
{
    public class PackagedCommandTests : TestBase
    {
        private readonly string _testProjectsRoot;
        private readonly string _desktopTestProjectsRoot;

        public PackagedCommandTests()
        {
            _testProjectsRoot = Path.Combine(AppContext.BaseDirectory, "TestAssets", "TestProjects");
            _desktopTestProjectsRoot = Path.Combine(AppContext.BaseDirectory, "TestAssets", "DesktopTestProjects");
        }

        [Theory]
        [InlineData("AppWithDirectAndToolDependency")]
        [InlineData("AppWithToolDependency")]
        public void TestProjectToolIsAvailableThroughDriver(string appName)
        {
            var appDirectory = Path.Combine(_testProjectsRoot, appName);

            new BuildCommand(Path.Combine(appDirectory, "project.json"))
                .Execute()
                .Should()
                .Pass();

            CommandResult result = new PortableCommand { WorkingDirectory = appDirectory }
                .ExecuteWithCapturedOutput();

            result.Should().HaveStdOut("Hello Portable World!" + Environment.NewLine);
            result.Should().NotHaveStdErr();
            result.Should().Pass();
        }

        [Fact]
        public void CanInvokeToolWhosePackageNameIsDifferentFromDllName()
        {
            var appDirectory = Path.Combine(_testProjectsRoot, "AppWithDependencyOnToolWithOutputName");

            new BuildCommand(Path.Combine(appDirectory, "project.json"))
                .Execute()
                .Should()
                .Pass();

            CommandResult result = new GenericCommand("tool-with-output-name") { WorkingDirectory = appDirectory }
                .ExecuteWithCapturedOutput();

            result.Should().HaveStdOutContaining("Tool with output name!");
            result.Should().NotHaveStdErr();
            result.Should().Pass();
        }

        [Fact]
        public void CanInvokeToolFromDirectDependenciesIfPackageNameDifferentFromToolName()
        {
            var appDirectory = Path.Combine(_testProjectsRoot, "AppWithDirectDependencyWithOutputName");
            const string framework = ".NETCoreApp,Version=v1.0";

            new BuildCommand(Path.Combine(appDirectory, "project.json"))
                .Execute()
                .Should()
                .Pass();

            CommandResult result = new DependencyToolInvokerCommand { WorkingDirectory = appDirectory }
                    .ExecuteWithCapturedOutput("tool-with-output-name", framework, string.Empty);

                result.Should().HaveStdOutContaining("Tool with output name!");
                result.Should().NotHaveStdErr();
                result.Should().Pass();
        }

        // need conditional theories so we can skip on non-Windows
        [Theory]
        [MemberData("DependencyToolArguments")]
        public void TestFrameworkSpecificDependencyToolsCanBeInvoked(string framework, string args, string expectedDependencyToolPath)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var appDirectory = Path.Combine(_desktopTestProjectsRoot, "AppWithDirectDependencyDesktopAndPortable");

            new BuildCommand(Path.Combine(appDirectory, "project.json"))
                .Execute()
                .Should()
                .Pass();

            CommandResult result = new DependencyToolInvokerCommand { WorkingDirectory = appDirectory }
                    .ExecuteWithCapturedOutput("desktop-and-portable", framework, args);

                result.Should().HaveStdOutContaining(framework);
                result.Should().HaveStdOutContaining(args);
                result.Should().HaveStdOutContaining(expectedDependencyToolPath);
                result.Should().NotHaveStdErr();
                result.Should().Pass();
        }

        [Fact]
        public void ToolsCanAccessDependencyContextProperly()
        {
            var appDirectory = Path.Combine(_testProjectsRoot, "DependencyContextFromTool");

            CommandResult result = new DependencyContextTestCommand() { WorkingDirectory = appDirectory }
                .Execute(Path.Combine(appDirectory, "project.json"));

            result.Should().Pass();
        }

        public static IEnumerable<object[]> DependencyToolArguments
        {
            get
            {
                var rid = PlatformServices.Default.Runtime.GetLegacyRestoreRuntimeIdentifier();
                var projectOutputPath  = $"AppWithDirectDependencyDesktopAndPortable\\bin\\Debug\\net451\\{rid}\\dotnet-desktop-and-portable.exe";
                return new[]
                {
                    new object[] { ".NETCoreApp,Version=v1.0", "CoreFX", "lib\\netcoreapp1.0\\dotnet-desktop-and-portable.dll" },
                    new object[] { ".NETFramework,Version=v4.5.1", "NetFX", projectOutputPath }
                };
            }
        }

        [Fact]
        public void TestProjectDependencyIsNotAvailableThroughDriver()
        {
            var appName = "AppWithDirectDependency";
            var appDirectory = Path.Combine(_testProjectsRoot, appName);

            new BuildCommand(Path.Combine(appDirectory, "project.json"))
                .Execute()
                .Should()
                .Pass();

            var currentDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(appDirectory);

            try
            {
                CommandResult result = new HelloCommand().ExecuteWithCapturedOutput();

                result.StdOut.Should().Contain("No executable found matching command");
                result.Should().Fail();
            }
            finally
            {
                Directory.SetCurrentDirectory(currentDirectory);
            }
        }

        class HelloCommand : TestCommand
        {
            public HelloCommand()
                : base("dotnet")
            {
            }

            public override CommandResult Execute(string args = "")
            {
                args = $"hello {args}";
                return base.Execute(args);
            }

            public override CommandResult ExecuteWithCapturedOutput(string args = "")
            {
                args = $"hello {args}";
                return base.ExecuteWithCapturedOutput(args);
            }
        }

        class PortableCommand : TestCommand
        {
            public PortableCommand()
                : base("dotnet")
            {
            }

            public override CommandResult Execute(string args = "")
            {
                args = $"portable {args}";
                return base.Execute(args);
            }

            public override CommandResult ExecuteWithCapturedOutput(string args = "")
            {
                args = $"portable {args}";
                return base.ExecuteWithCapturedOutput(args);
            }
        }

        class GenericCommand : TestCommand
        {
            private readonly string _commandName;

            public GenericCommand(string commandName)
                : base("dotnet")
            {
                _commandName = commandName;
            }

            public override CommandResult Execute(string args = "")
            {
                args = $"{_commandName} {args}";
                return base.Execute(args);
            }

            public override CommandResult ExecuteWithCapturedOutput(string args = "")
            {
                args = $"{_commandName} {args}";
                return base.ExecuteWithCapturedOutput(args);
            }
        }

        class DependencyContextTestCommand : TestCommand
        {
            public DependencyContextTestCommand()
                : base("dotnet")
            {
            }

            public override CommandResult Execute(string path)
            {
                var args = $"dependency-context-test {path}";
                return base.Execute(args);
            }

            public override CommandResult ExecuteWithCapturedOutput(string path)
            {
                var args = $"dependency-context-test {path}";
                return base.ExecuteWithCapturedOutput(args);
            }
        }
    }
}
