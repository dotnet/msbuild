// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.DotNet.InternalAbstractions;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Build.Construction;
using System.Linq;
using Microsoft.Build.Evaluation;

namespace Microsoft.DotNet.Tests
{
    public class PackagedCommandTests : TestBase
    {
        private readonly ITestOutputHelper _output;

        public PackagedCommandTests(ITestOutputHelper output)
        {
            _output = output;
        }

        public static IEnumerable<object[]> DependencyToolArguments
        {
            get
            {
                var rid = DotnetLegacyRuntimeIdentifiers.InferLegacyRestoreRuntimeIdentifier();
                var projectOutputPath = $"AppWithProjTool2Fx\\bin\\Debug\\net451\\{rid}\\dotnet-desktop-and-portable.exe";
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
                var rid = DotnetLegacyRuntimeIdentifiers.InferLegacyRestoreRuntimeIdentifier();

                var projectOutputPath = $"LibWithProjTool2Fx\\bin\\Debug\\net451\\dotnet-desktop-and-portable.exe";

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
            var testInstance = TestAssets.Get(appName)
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles();

            new BuildCommand()
                .WithProjectDirectory(testInstance.Root)
                .Execute()
                .Should().Pass();

            new PortableCommand()
                .WithWorkingDirectory(testInstance.Root)
                .ExecuteWithCapturedOutput()
                .Should().HaveStdOutContaining("Hello Portable World!")
                     .And.NotHaveStdErr()
                     .And.Pass();
        }

        [Fact]
        public void CanInvokeToolWhosePackageNameIsDifferentFromDllName()
        {
            var testInstance = TestAssets.Get("AppWithDepOnToolWithOutputName")
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles();

            new BuildCommand()
                .WithProjectDirectory(testInstance.Root)
                .Execute()
                .Should().Pass();

            new GenericCommand("tool-with-output-name")
                .WithWorkingDirectory(testInstance.Root)
                .ExecuteWithCapturedOutput()
                .Should().HaveStdOutContaining("Tool with output name!")
                     .And.NotHaveStdErr()
                     .And.Pass();
        }

        [Fact]
        public void CanInvokeToolFromDirectDependenciesIfPackageNameDifferentFromToolName()
        {
            var testInstance = TestAssets.Get("AppWithDirectDepWithOutputName")
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles();
            
            const string framework = ".NETCoreApp,Version=v1.0";

            new BuildCommand()
                .WithProjectDirectory(testInstance.Root)
                .WithConfiguration("Debug")
                .Execute()
                .Should().Pass();

            new DependencyToolInvokerCommand()
                .WithWorkingDirectory(testInstance.Root)
                .WithEnvironmentVariable(CommandContext.Variables.Verbose, "true")
                .ExecuteWithCapturedOutput($"tool-with-output-name", framework, "")
                .Should().HaveStdOutContaining("Tool with output name!")
                     .And.NotHaveStdErr()
                     .And.Pass();
        }

        [Fact]
        public void ItShowsErrorWhenToolIsNotRestored()
        {
            var testInstance = TestAssets.Get("NonRestoredTestProjects", "AppWithNonExistingToolDependency")
                .CreateInstance()
                .WithSourceFiles();

            new TestCommand("dotnet")
                .WithWorkingDirectory(testInstance.Root)
                .ExecuteWithCapturedOutput("nonexistingtool")
                .Should().Fail()
                    .And.HaveStdErrContaining("Version for package `dotnet-nonexistingtool` could not be resolved.");
        }

        [Fact]
        public void ItRunsToolRestoredToSpecificPackageDir()
        {
            var testInstance = TestAssets.Get("NonRestoredTestProjects", "ToolWithRandomPackageName")
                .CreateInstance()
                .WithSourceFiles();

            var appWithDepOnToolDir = testInstance.Root.Sub("AppWithDepOnTool");
            var toolWithRandPkgNameDir = testInstance.Root.Sub("ToolWithRandomPackageName");
            var pkgsDir = testInstance.Root.CreateSubdirectory("pkgs");

            string randomPackageName = Guid.NewGuid().ToString();

            // TODO: This is a workround for https://github.com/dotnet/cli/issues/5020
            SetGeneratedPackageName(appWithDepOnToolDir.GetFile("AppWithDepOnTool.csproj"),
                                    randomPackageName);

            SetGeneratedPackageName(toolWithRandPkgNameDir.GetFile("ToolWithRandomPackageName.csproj"),
                                    randomPackageName);

            new RestoreCommand()
                .WithWorkingDirectory(toolWithRandPkgNameDir)
                .ExecuteWithCapturedOutput()
                .Should().Pass()
                .And.NotHaveStdErr();

            new PackCommand()
                .WithWorkingDirectory(toolWithRandPkgNameDir)
                .ExecuteWithCapturedOutput($"-o \"{pkgsDir.FullName}\"")
                .Should().Pass()
                .And.NotHaveStdErr();

            new RestoreCommand()
                .WithWorkingDirectory(appWithDepOnToolDir)
                .ExecuteWithCapturedOutput($"--packages \"{pkgsDir.FullName}\"")
                .Should().Pass()
                .And.NotHaveStdErr();

            new TestCommand("dotnet")
                .WithWorkingDirectory(appWithDepOnToolDir)
                .ExecuteWithCapturedOutput("randompackage")
                .Should().Pass()
                .And.HaveStdOutContaining("Hello World from tool!")
                .And.NotHaveStdErr();
        }

        // need conditional theories so we can skip on non-Windows
        //[Theory(Skip="https://github.com/dotnet/cli/issues/4514")]
        //[MemberData("DependencyToolArguments")]
        public void TestFrameworkSpecificDependencyToolsCanBeInvoked(string identifier, string framework, string expectedDependencyToolPath, bool windowsOnly)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && windowsOnly)
            {
                return;
            }

            var testInstance = TestAssets.Get(TestAssetKinds.DesktopTestProjects, "AppWithProjTool2Fx")
                .CreateInstance(identifier: identifier)
                .WithSourceFiles()
                .WithRestoreFiles();

            new BuildCommand()
                .WithWorkingDirectory(testInstance.Root)
                .WithConfiguration("Debug")
                .Execute()
                .Should().Pass();

            new DependencyToolInvokerCommand()
                .WithWorkingDirectory(testInstance.Root)
                .ExecuteWithCapturedOutput($"desktop-and-portable {framework} {identifier}")
                .Should().HaveStdOutContaining(framework)
                    .And.HaveStdOutContaining(identifier)
                    .And.HaveStdOutContaining(expectedDependencyToolPath)
                    .And.NotHaveStdErr()
                    .And.Pass();
        }

        [Theory]
        [MemberData("LibraryDependencyToolArguments")]
        public void TestFrameworkSpecificLibraryDependencyToolsCannotBeInvoked(string identifier, string framework, string expectedDependencyToolPath, bool windowsOnly)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && windowsOnly)
            {
                return;
            }

            var testInstance = TestAssets.Get(TestAssetKinds.DesktopTestProjects, "LibWithProjTool2Fx")
                .CreateInstance(identifier: identifier)
                .WithSourceFiles()
                .WithRestoreFiles();

            new BuildCommand()
                .WithWorkingDirectory(testInstance.Root)
                .WithConfiguration("Debug")
                .Execute()
                .Should().Pass();

            new DependencyToolInvokerCommand()
                .WithWorkingDirectory(testInstance.Root)
                .ExecuteWithCapturedOutput($"desktop-and-portable {framework} {identifier}")
                .Should().Fail();
        }

        [Fact]
        public void ToolsCanAccessDependencyContextProperly()
        {
            var testInstance = TestAssets.Get("DependencyContextFromTool")
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles();

            new DependencyContextTestCommand()
                .WithWorkingDirectory(testInstance.Root)
                .Execute("")
                .Should().Pass();
        }

        [Fact]
        public void TestProjectDependencyIsNotAvailableThroughDriver()
        {
            var testInstance = TestAssets.Get("AppWithDirectDep")
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles();

            new BuildCommand()
                .WithWorkingDirectory(testInstance.Root)
                .WithFramework(NuGet.Frameworks.FrameworkConstants.CommonFrameworks.NetCoreApp10)
                .Execute()
                .Should().Pass();

            var currentDirectory = Directory.GetCurrentDirectory();

            CommandResult result = new HelloCommand()
                .WithWorkingDirectory(testInstance.Root)
                .ExecuteWithCapturedOutput();

            result.StdErr.Should().Contain("No executable found matching command");
            
            result.Should().Fail();        
        }

        [Fact]
        public void WhenToolAssetsFileIsInUseThenCLIRetriesLaunchingTheCommandForAtLeastOneSecond()
        {
            var testInstance = TestAssets.Get("AppWithToolDependency")
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles();

            var assetsFile = new DirectoryInfo(new RepoDirectoriesProvider().NugetPackages)
                .GetDirectory(".tools", "dotnet-portable", "1.0.0", "netcoreapp1.0")
                .GetFile("project.assets.json");

            var stopWatch = Stopwatch.StartNew();

            using (assetsFile.Lock()
                             .DisposeAfter(TimeSpan.FromMilliseconds(1000)))
            {
                new PortableCommand()
                    .WithWorkingDirectory(testInstance.Root)
                    .ExecuteWithCapturedOutput()
                    .Should().HaveStdOutContaining("Hello Portable World!")
                        .And.NotHaveStdErr()
                        .And.Pass();
            }

            stopWatch.Stop();

            stopWatch.ElapsedMilliseconds.Should().BeGreaterThan(1000, "Because dotnet should respect the NuGet lock");
        }

        [Fact]
        public void WhenToolAssetsFileIsLockedByNuGetThenCLIRetriesLaunchingTheCommandForAtLeastOneSecond()
        {
            var testInstance = TestAssets.Get("AppWithToolDependency")
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles();

            var assetsFile = new DirectoryInfo(new RepoDirectoriesProvider().NugetPackages)
                .GetDirectory(".tools", "dotnet-portable", "1.0.0", "netcoreapp1.0")
                .GetFile("project.assets.json");

            var stopWatch = Stopwatch.StartNew();

            using (assetsFile.NuGetLock()
                             .DisposeAfter(TimeSpan.FromMilliseconds(1000)))
            {
                new PortableCommand()
                    .WithWorkingDirectory(testInstance.Root)
                    .ExecuteWithCapturedOutput()
                    .Should().HaveStdOutContaining("Hello Portable World!")
                        .And.NotHaveStdErr()
                        .And.Pass();
            }

            stopWatch.Stop();

            stopWatch.ElapsedMilliseconds.Should().BeGreaterThan(1000, "Because dotnet should respect the NuGet lock");
        }

        private void SetGeneratedPackageName(FileInfo project, string packageName)
        {
            const string propertyName = "GeneratedPackageId";
            var p = ProjectRootElement.Open(project.FullName, new ProjectCollection(), true);
            p.AddProperty(propertyName, packageName);
            p.Save();
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
