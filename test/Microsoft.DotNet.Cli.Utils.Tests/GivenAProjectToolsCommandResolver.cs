// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.Versioning;
using Xunit;

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    public class GivenAProjectToolsCommandResolver : TestBase
    {
        private static readonly NuGetFramework s_toolPackageFramework = FrameworkConstants.CommonFrameworks.NetCoreApp10;

        private const string TestProjectName = "AppWithToolDependency";

        [Fact]
        public void It_returns_null_when_CommandName_is_null()
        {
            var projectToolsCommandResolver = SetupProjectToolsCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = null,
                CommandArguments = new string[] { "" },
                ProjectDirectory = "/some/directory"
            };

            var result = projectToolsCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void It_returns_null_when_ProjectDirectory_is_null()
        {
            var projectToolsCommandResolver = SetupProjectToolsCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "command",
                CommandArguments = new string[] { "" },
                ProjectDirectory = null
            };

            var result = projectToolsCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void It_returns_null_when_CommandName_does_not_exist_in_ProjectTools()
        {
            var projectToolsCommandResolver = SetupProjectToolsCommandResolver();

            var testInstance = TestAssetsManager.CreateTestInstance(TestProjectName)
                .WithLockFiles();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "nonexistent-command",
                CommandArguments = null,
                ProjectDirectory = testInstance.Path
            };

            var result = projectToolsCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void It_returns_a_CommandSpec_with_DOTNET_as_FileName_and_CommandName_in_Args_when_CommandName_exists_in_ProjectTools()
        {
            var projectToolsCommandResolver = SetupProjectToolsCommandResolver();

            var testInstance = TestAssetsManager.CreateTestInstance(TestProjectName)
                .WithLockFiles();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "dotnet-portable",
                CommandArguments = null,
                ProjectDirectory = testInstance.Path
            };

            var result = projectToolsCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();

            var commandFile = Path.GetFileNameWithoutExtension(result.Path);

            commandFile.Should().Be("dotnet");

            result.Args.Should().Contain(commandResolverArguments.CommandName);
        }

        [Fact]
        public void It_escapes_CommandArguments_when_returning_a_CommandSpec()
        {
            var projectToolsCommandResolver = SetupProjectToolsCommandResolver();

            var testInstance = TestAssetsManager.CreateTestInstance(TestProjectName)
                .WithLockFiles();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "dotnet-portable",
                CommandArguments = new[] { "arg with space" },
                ProjectDirectory = testInstance.Path
            };

            var result = projectToolsCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();
            result.Args.Should().Contain("\"arg with space\"");
        }

        [Fact]
        public void It_returns_a_CommandSpec_with_Args_containing_CommandPath_when_returning_a_CommandSpec_and_CommandArguments_are_null()
        {
            var projectToolsCommandResolver = SetupProjectToolsCommandResolver();

            var testInstance = TestAssetsManager.CreateTestInstance(TestProjectName)
                .WithLockFiles();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "dotnet-portable",
                CommandArguments = null,
                ProjectDirectory = testInstance.Path
            };

            var result = projectToolsCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();

            var commandPath = result.Args.Trim('"');
            commandPath.Should().Contain("dotnet-portable.dll");
        }

        [Fact]
        public void It_writes_a_deps_json_file_next_to_the_lockfile()
        {
            var projectToolsCommandResolver = SetupProjectToolsCommandResolver();

            var testInstance = TestAssetsManager.CreateTestInstance(TestProjectName)
                .WithLockFiles();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "dotnet-portable",
                CommandArguments = null,
                ProjectDirectory = testInstance.Path
            };

            var context = ProjectContext.Create(Path.Combine(testInstance.Path, "project.json"), s_toolPackageFramework);

            var nugetPackagesRoot = context.PackagesDirectory;
            var toolPathCalculator = new ToolPathCalculator(nugetPackagesRoot);

            var lockFilePath = toolPathCalculator.GetLockFilePath(
                "dotnet-portable",
                new NuGetVersion("1.0.0"),
                s_toolPackageFramework);

            var directory = Path.GetDirectoryName(lockFilePath);

            var depsJsonFile = Directory
                .EnumerateFiles(directory)
                .FirstOrDefault(p => Path.GetFileName(p).EndsWith(FileNameSuffixes.DepsJson));

            if (depsJsonFile != null)
            {
                File.Delete(depsJsonFile);
            }

            var result = projectToolsCommandResolver.Resolve(commandResolverArguments);
            result.Should().NotBeNull();


            depsJsonFile = Directory
                .EnumerateFiles(directory)
                .FirstOrDefault(p => Path.GetFileName(p).EndsWith(FileNameSuffixes.DepsJson));

            depsJsonFile.Should().NotBeNull();
        }

        [Fact]
        public void Generate_deps_json_method_doesnt_overwrite_when_deps_file_already_exists()
        {
            var testInstance = TestAssetsManager.CreateTestInstance(TestProjectName)
                .WithLockFiles();

            var context = ProjectContext.Create(Path.Combine(testInstance.Path, "project.json"), s_toolPackageFramework);

            var nugetPackagesRoot = context.PackagesDirectory;
            var toolPathCalculator = new ToolPathCalculator(nugetPackagesRoot);

            var lockFilePath = toolPathCalculator.GetLockFilePath(
                "dotnet-portable",
                new NuGetVersion("1.0.0"),
                s_toolPackageFramework);

            var lockFile = new LockFileFormat().Read(lockFilePath);

            var depsJsonFile = Path.Combine(
                Path.GetDirectoryName(lockFilePath),
                "dotnet-portable.deps.json");

            if (File.Exists(depsJsonFile))
            {
                File.Delete(depsJsonFile);
            }
            File.WriteAllText(depsJsonFile, "temp");

            var projectToolsCommandResolver = SetupProjectToolsCommandResolver();
            projectToolsCommandResolver.GenerateDepsJsonFile(lockFile, depsJsonFile);

            File.ReadAllText(depsJsonFile).Should().Be("temp");
            File.Delete(depsJsonFile);
        }

        private ProjectToolsCommandResolver SetupProjectToolsCommandResolver(
            IPackagedCommandSpecFactory packagedCommandSpecFactory = null)
        {
            packagedCommandSpecFactory = packagedCommandSpecFactory ?? new PackagedCommandSpecFactory();

            var projectToolsCommandResolver = new ProjectToolsCommandResolver(packagedCommandSpecFactory);

            return projectToolsCommandResolver;
        }
    }
}
