// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;
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

        // need conditional theories so we can skip on non-Windows
        [Theory]
        [InlineData(".NETStandardApp,Version=v1.5", "CoreFX")]
        [InlineData(".NETFramework,Version=v4.5.1", "NetFX")]
        public void TestFrameworkSpecificDependencyToolsCanBeInvoked(string framework, string args)
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
                    .ExecuteWithCapturedOutput(framework, args);

                result.Should().HaveStdOutContaining(framework);
                result.Should().HaveStdOutContaining(args);
                result.Should().NotHaveStdErr();
                result.Should().Pass();
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

        class DependencyToolInvokerCommand : TestCommand
        {
            public DependencyToolInvokerCommand()
                : base("dotnet")
            {
            }

            public CommandResult Execute(string framework, string additionalArgs)
            {
                var args = $"dependency-tool-invoker desktop-and-portable --framework {framework} {additionalArgs}";
                return base.Execute(args);
            }

            public CommandResult ExecuteWithCapturedOutput(string framework, string additionalArgs)
            {
                var args = $"dependency-tool-invoker desktop-and-portable --framework {framework} {additionalArgs}";
                return base.ExecuteWithCapturedOutput(args);
            }
        }
    }
}
