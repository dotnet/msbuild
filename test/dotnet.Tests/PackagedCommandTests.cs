// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.DotNet.InternalAbstractions;
using Xunit;

namespace Microsoft.DotNet.Tests
{
    public class PackagedCommandTests : TestBase
    {
        private readonly TestAssetsManager _desktopTestAssetsManager = GetTestGroupTestAssetsManager("DesktopTestProjects");

        public static IEnumerable<object[]> DependencyToolArguments
        {
            get
            {
                var rid = DotnetRuntimeIdentifiers.InferLegacyRestoreRuntimeIdentifier();
                var projectOutputPath = $"AppWithDirectDepDesktopAndPortable\\bin\\Debug\\net451\\{rid}\\dotnet-desktop-and-portable.exe";
                return new[]
                {
                    new object[] { "CoreFX", ".NETCoreApp,Version=v1.0", "lib\\netcoreapp1.0\\dotnet-desktop-and-portable.dll", true },
                    new object[] { "NetFX", ".NETFramework,Version=v4.5.1", projectOutputPath, true }
                };
            }
        }

        public static IEnumerable<object[]> LibraryDependencyToolArguments
        {
            get
            {
                var rid = DotnetRuntimeIdentifiers.InferLegacyRestoreRuntimeIdentifier();
                var projectOutputPath = $"LibraryWithDirectDependencyDesktopAndPortable\\bin\\Debug\\net451\\dotnet-desktop-and-portable.exe";
                return new[]
                {
                    new object[] { "CoreFX", ".NETStandard,Version=v1.6", "lib\\netstandard1.6\\dotnet-desktop-and-portable.dll", true },
                    new object[] { "NetFX", ".NETFramework,Version=v4.5.1", projectOutputPath, true }
                };
            }
        }

        [Theory]
        [InlineData("AppWithDirectAndToolDep")]
        [InlineData("AppWithToolDependency")]
        public void TestProjectToolIsAvailableThroughDriver(string appName)
        {
            var testInstance = TestAssetsManager
                .CreateTestInstance(appName, identifier: appName)
                .WithLockFiles();

            var appDirectory = testInstance.Path;

            new BuildCommand(Path.Combine(appDirectory, "project.json"))
                .Execute()
                .Should()
                .Pass();

            CommandResult result = new PortableCommand { WorkingDirectory = appDirectory }
                .ExecuteWithCapturedOutput();

            result.Should().HaveStdOutContaining("Hello Portable World!" + Environment.NewLine);
            result.Should().NotHaveStdErr();
            result.Should().Pass();
        }

        [Fact]
        public void CanInvokeToolWhosePackageNameIsDifferentFromDllName()
        {
            var testInstance = TestAssetsManager
                .CreateTestInstance("AppWithDepOnToolWithOutputName")
                .WithLockFiles();

            var appDirectory = testInstance.Path;

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
            var testInstance = TestAssetsManager
                .CreateTestInstance("AppWithDirectDepWithOutputName")
                .WithBuildArtifacts()
                .WithLockFiles();

            var appDirectory = testInstance.Path;
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
        public void TestFrameworkSpecificDependencyToolsCanBeInvoked(string identifier, string framework, string expectedDependencyToolPath, bool windowsOnly)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && windowsOnly)
            {
                return;
            }

            var testInstance = _desktopTestAssetsManager
                .CreateTestInstance("AppWithDirectDepDesktopAndPortable", identifier: identifier)
                .WithBuildArtifacts()
                .WithLockFiles();

            var appDirectory = testInstance.Path;

            new BuildCommand(Path.Combine(appDirectory, "project.json"))
                .Execute()
                .Should()
                .Pass();

            CommandResult result = new DependencyToolInvokerCommand { WorkingDirectory = appDirectory }
                    .ExecuteWithCapturedOutput("desktop-and-portable", framework, identifier);

            result.Should().HaveStdOutContaining(framework);
            result.Should().HaveStdOutContaining(identifier);
            result.Should().HaveStdOutContaining(expectedDependencyToolPath);
            result.Should().NotHaveStdErr();
            result.Should().Pass();
        }

        [Theory]
        [MemberData("LibraryDependencyToolArguments")]
        public void TestFrameworkSpecificLibraryDependencyToolsCannotBeInvoked(string identifier, string framework, string expectedDependencyToolPath, bool windowsOnly)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && windowsOnly)
            {
                return;
            }
            
            var testInstance = _desktopTestAssetsManager
                .CreateTestInstance("LibraryWithDirectDependencyDesktopAndPortable", identifier: identifier)
                .WithLockFiles();

            var appDirectory = testInstance.Path;

            new BuildCommand(Path.Combine(appDirectory, "project.json"))
                .Execute()
                .Should()
                .Pass();

            CommandResult result = new DependencyToolInvokerCommand { WorkingDirectory = appDirectory }
                    .ExecuteWithCapturedOutput("desktop-and-portable", framework, identifier);

            result.Should().HaveStdOutContaining("Command not found");
            result.Should().Fail();
        }

        [Fact]
        public void ToolsCanAccessDependencyContextProperly()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("DependencyContextFromTool").WithLockFiles();

            var appDirectory = testInstance.Path;

            CommandResult result = new DependencyContextTestCommand() { WorkingDirectory = appDirectory }
                .Execute(Path.Combine(appDirectory, "project.json"));

            result.Should().Pass();
        }

        [Fact]
        public void TestProjectDependencyIsNotAvailableThroughDriver()
        {
            var testInstance = TestAssetsManager
                .CreateTestInstance("AppWithDirectDep")
                .WithLockFiles();

            var appDirectory = testInstance.Path;

            new BuildCommand(Path.Combine(appDirectory, "project.json"))
                .Execute()
                .Should()
                .Pass();

            var currentDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(appDirectory);

            try
            {
                CommandResult result = new HelloCommand().ExecuteWithCapturedOutput();

                result.StdErr.Should().Contain("No executable found matching command");
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
