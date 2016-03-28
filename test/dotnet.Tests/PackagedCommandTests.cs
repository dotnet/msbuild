// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using FluentAssertions;

namespace Microsoft.DotNet.Tests
{
    public class PackagedCommandTests : TestBase
    {
        private readonly string _testProjectsRoot;

        public PackagedCommandTests()
        {
            _testProjectsRoot = Path.Combine(AppContext.BaseDirectory, "TestAssets", "TestProjects");
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

            var currentDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(appDirectory);

            try
            {
                CommandResult result = new PortableCommand().ExecuteWithCapturedOutput();

                result.Should().HaveStdOut("Hello Portable World!" + Environment.NewLine);
                result.Should().NotHaveStdErr();
                result.Should().Pass();
            }
            finally
            {
                Directory.SetCurrentDirectory(currentDirectory);
            }
        }

        [Theory]
        [InlineData(".NETStandardApp,Version=v1.5")]
        [InlineData(".NETFramework,Version=v4.5.1")]
        public void TestFrameworkSpecificDependencyToolsCanBeInvoked(string framework)
        {
            var appDirectory = Path.Combine(_testProjectsRoot, "AppWithDirectDependencyDesktopAndPortable");

            new BuildCommand(Path.Combine(appDirectory, "project.json"))
                .Execute()
                .Should()
                .Pass();

            var currentDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(appDirectory);

            try
            {
                CommandResult result = new DependencyToolInvokerCommand()
                    .ExecuteWithCapturedOutput(framework);

                result.Should().HaveStdOutContaining(framework);
                result.Should().NotHaveStdErr();
                result.Should().Pass();
            }
            finally
            {
                Directory.SetCurrentDirectory(currentDirectory);
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
                result.Should().NotPass();
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

            public override CommandResult Execute(string framework)
            {
                var args = $"dependency-tool-invoker desktop-and-portable --framework {framework}";
                return base.Execute(args);
            }

            public override CommandResult ExecuteWithCapturedOutput(string framework)
            {
                var args = $"dependency-tool-invoker desktop-and-portable --framework {framework}";
                return base.ExecuteWithCapturedOutput(args);
            }
        }
    }
}
