// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using NuGet.Frameworks;
using Xunit;

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    public class GivenAProjectDependenciesCommandFactory : TestBase
    {
        private static readonly NuGetFramework s_desktopTestFramework = FrameworkConstants.CommonFrameworks.Net451;

        private RepoDirectoriesProvider _repoDirectoriesProvider;

        public GivenAProjectDependenciesCommandFactory()
        {
            _repoDirectoriesProvider = new RepoDirectoriesProvider();
            Environment.SetEnvironmentVariable(
                Constants.MSBUILD_EXE_PATH,
                Path.Combine(_repoDirectoriesProvider.Stage2Sdk, "MSBuild.dll"));
        }

        [WindowsOnlyFact]
        public void It_resolves_desktop_apps_defaulting_to_Debug_Configuration()
        {
            var configuration = "Debug";

            var testInstance = TestAssets.Get(TestAssetKinds.DesktopTestProjects, "AppWithProjTool2Fx")
                .CreateInstance()
                .WithSourceFiles()
                .WithNuGetConfig(_repoDirectoriesProvider.TestPackages);

            var restoreCommand = new RestoreCommand()
                .WithWorkingDirectory(testInstance.Root)
                .ExecuteWithCapturedOutput()
                .Should().Pass();

            var buildCommand = new BuildCommand()
                .WithWorkingDirectory(testInstance.Root)
                .WithConfiguration(configuration)
                .WithCapturedOutput()
                .Execute()
                .Should().Pass();
                
            var factory = new ProjectDependenciesCommandFactory(
                s_desktopTestFramework,
                null,
                null,
                null,
                testInstance.Root.FullName);

            var command = factory.Create("dotnet-desktop-and-portable", null);

            command.CommandName.Should().Contain(testInstance.Root.GetDirectory("bin", configuration).FullName);

            Path.GetFileName(command.CommandName).Should().Be("dotnet-desktop-and-portable.exe");
        }

        [WindowsOnlyFact]
        public void It_resolves_desktop_apps_when_configuration_is_Debug()
        {
            var configuration = "Debug";

            var testInstance = TestAssets.Get(TestAssetKinds.DesktopTestProjects, "AppWithProjTool2Fx")
                .CreateInstance()
                .WithSourceFiles()
                .WithNuGetConfig(_repoDirectoriesProvider.TestPackages);

            var restoreCommand = new RestoreCommand()
                .WithWorkingDirectory(testInstance.Root)
                .ExecuteWithCapturedOutput()
                .Should().Pass();

            var buildCommand = new BuildCommand()
                .WithWorkingDirectory(testInstance.Root)
                .WithConfiguration(configuration)
                .Execute()
                .Should().Pass();

            var factory = new ProjectDependenciesCommandFactory(
                s_desktopTestFramework,
                configuration,
                null,
                null,
                testInstance.Root.FullName);

            var command = factory.Create("dotnet-desktop-and-portable", null);

            command.CommandName.Should().Contain(testInstance.Root.GetDirectory("bin", configuration).FullName);
            Path.GetFileName(command.CommandName).Should().Be("dotnet-desktop-and-portable.exe");
        }

        [WindowsOnlyFact]
        public void It_resolves_desktop_apps_when_configuration_is_Release()
        {
            var configuration = "Debug";

            var testInstance = TestAssets.Get(TestAssetKinds.DesktopTestProjects, "AppWithProjTool2Fx")
                .CreateInstance()
                .WithSourceFiles()
                .WithNuGetConfig(_repoDirectoriesProvider.TestPackages);

            var restoreCommand = new RestoreCommand()
                .WithWorkingDirectory(testInstance.Root)
                .ExecuteWithCapturedOutput()
                .Should().Pass();

            var buildCommand = new BuildCommand()
                .WithWorkingDirectory(testInstance.Root)
                .WithConfiguration(configuration)
                .WithCapturedOutput()
                .Execute()
                .Should().Pass();

            var factory = new ProjectDependenciesCommandFactory(
                s_desktopTestFramework,
                configuration,
                null,
                null,
                testInstance.Root.FullName);

            var command = factory.Create("dotnet-desktop-and-portable", null);

            command.CommandName.Should().Contain(testInstance.Root.GetDirectory("bin", configuration).FullName);

            Path.GetFileName(command.CommandName).Should().Be("dotnet-desktop-and-portable.exe");
        }

        [WindowsOnlyFact]
        public void It_resolves_desktop_apps_using_configuration_passed_to_create()
        {
            var configuration = "Debug";

            var testInstance = TestAssets.Get(TestAssetKinds.DesktopTestProjects, "AppWithProjTool2Fx")
                .CreateInstance()
                .WithSourceFiles()
                .WithNuGetConfig(_repoDirectoriesProvider.TestPackages);

            var restoreCommand = new RestoreCommand()
                .WithWorkingDirectory(testInstance.Root)
                .ExecuteWithCapturedOutput()
                .Should().Pass();

            var buildCommand = new BuildCommand()
                .WithWorkingDirectory(testInstance.Root)
                .WithConfiguration(configuration)
                .WithCapturedOutput()
                .Execute()
                .Should().Pass();

            var factory = new ProjectDependenciesCommandFactory(
                s_desktopTestFramework,
                "Debug",
                null,
                null,
                testInstance.Root.FullName);

            var command = factory.Create("dotnet-desktop-and-portable", null, configuration: configuration);

            command.CommandName.Should().Contain(testInstance.Root.GetDirectory("bin", configuration).FullName);

            Path.GetFileName(command.CommandName).Should().Be("dotnet-desktop-and-portable.exe");
        }

        [Fact]
        public void It_resolves_tools_whose_package_name_is_different_than_dll_name()
        {
            Environment.SetEnvironmentVariable(
                Constants.MSBUILD_EXE_PATH,
                Path.Combine(new RepoDirectoriesProvider().Stage2Sdk, "MSBuild.dll"));

            var configuration = "Debug";

            var testInstance = TestAssets.Get("AppWithDirectDepWithOutputName")
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles();

            var buildCommand = new BuildCommand()
                .WithProjectDirectory(testInstance.Root)
                .WithConfiguration(configuration)
                .WithCapturedOutput()
                .Execute()
                .Should().Pass();

            var factory = new ProjectDependenciesCommandFactory(
                FrameworkConstants.CommonFrameworks.NetCoreApp20,
                configuration,
                null,
                null,
                testInstance.Root.FullName);

            var command = factory.Create("dotnet-tool-with-output-name", null);

            command.CommandArgs.Should().Contain(
                Path.Combine("toolwithoutputname", "1.0.0", "lib", "netcoreapp2.1", "dotnet-tool-with-output-name.dll"));
        }
    }
}
