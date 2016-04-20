// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
using Xunit;
using Moq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.DotNet.TestFramework;
using System.Threading;
using FluentAssertions;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    public class GivenAProjectDependenciesCommandFactory : TestBase
    {
        private static readonly NuGetFramework s_desktopTestFramework = FrameworkConstants.CommonFrameworks.Net451;  
        
        [WindowsOnlyFact]
        public void It_resolves_desktop_apps_defaulting_to_Debug_Configuration()
        {
            var configuration = "Debug";

            var testAssetManager = new TestAssetsManager(Path.Combine(RepoRoot, "TestAssets", "DesktopTestProjects"));
            var testInstance = testAssetManager.CreateTestInstance("AppWithDirectDependencyDesktopAndPortable")
                .WithLockFiles();

            var buildCommand = new BuildCommand(
                Path.Combine(testInstance.TestRoot, "project.json"), 
                configuration: configuration)
                    .ExecuteWithCapturedOutput()
                    .Should()
                    .Pass();

            var context = ProjectContext.Create(testInstance.TestRoot, s_desktopTestFramework);

            var factory = new ProjectDependenciesCommandFactory(
                s_desktopTestFramework,
                null,
                null,
                null,
                testInstance.TestRoot);

            var command = factory.Create("dotnet-desktop-and-portable", null);

            command.CommandName.Should().Contain(Path.Combine(testInstance.TestRoot, "bin", configuration));
            Path.GetFileName(command.CommandName).Should().Be("dotnet-desktop-and-portable.exe");
        }

        [WindowsOnlyFact]
        public void It_resolves_desktop_apps_when_configuration_is_Debug()
        {
            var configuration = "Debug";

            var testAssetManager = new TestAssetsManager(Path.Combine(RepoRoot, "TestAssets", "DesktopTestProjects"));
            var testInstance = testAssetManager.CreateTestInstance("AppWithDirectDependencyDesktopAndPortable")
                .WithLockFiles();

            var buildCommand = new BuildCommand(
                Path.Combine(testInstance.TestRoot, "project.json"), 
                configuration: configuration)
                    .ExecuteWithCapturedOutput()
                    .Should()
                    .Pass();

            var context = ProjectContext.Create(testInstance.TestRoot, s_desktopTestFramework);

            var factory = new ProjectDependenciesCommandFactory(
                s_desktopTestFramework,
                configuration,
                null,
                null,
                testInstance.TestRoot);

            var command = factory.Create("dotnet-desktop-and-portable", null);

            command.CommandName.Should().Contain(Path.Combine(testInstance.TestRoot, "bin", configuration));
            Path.GetFileName(command.CommandName).Should().Be("dotnet-desktop-and-portable.exe");
        }

        [WindowsOnlyFact]
        public void It_resolves_desktop_apps_when_configuration_is_Release()
        {
            var configuration = "Release";

            var testAssetManager = new TestAssetsManager(Path.Combine(RepoRoot, "TestAssets", "DesktopTestProjects"));
            var testInstance = testAssetManager.CreateTestInstance("AppWithDirectDependencyDesktopAndPortable")
                .WithLockFiles();

            var buildCommand = new BuildCommand(
                Path.Combine(testInstance.TestRoot, "project.json"), 
                configuration: configuration)
                    .ExecuteWithCapturedOutput()
                    .Should()
                    .Pass();

            var context = ProjectContext.Create(testInstance.TestRoot, s_desktopTestFramework);

            var factory = new ProjectDependenciesCommandFactory(
                s_desktopTestFramework,
                configuration,
                null,
                null,
                testInstance.TestRoot);

            var command = factory.Create("dotnet-desktop-and-portable", null);

            command.CommandName.Should().Contain(Path.Combine(testInstance.TestRoot, "bin", configuration));
            Path.GetFileName(command.CommandName).Should().Be("dotnet-desktop-and-portable.exe");
        }

        [WindowsOnlyFact]
        public void It_resolves_desktop_apps_using_configuration_passed_to_create()
        {
            var configuration = "Release";

            var testAssetManager = new TestAssetsManager(Path.Combine(RepoRoot, "TestAssets", "DesktopTestProjects"));
            var testInstance = testAssetManager.CreateTestInstance("AppWithDirectDependencyDesktopAndPortable")
                .WithLockFiles();

            var buildCommand = new BuildCommand(
                Path.Combine(testInstance.TestRoot, "project.json"), 
                configuration: configuration)
                    .ExecuteWithCapturedOutput()
                    .Should()
                    .Pass();

            var context = ProjectContext.Create(testInstance.TestRoot, s_desktopTestFramework);

            var factory = new ProjectDependenciesCommandFactory(
                s_desktopTestFramework,
                "Debug",
                null,
                null,
                testInstance.TestRoot);

            var command = factory.Create("dotnet-desktop-and-portable", null, configuration: configuration);

            command.CommandName.Should().Contain(Path.Combine(testInstance.TestRoot, "bin", configuration));
            Path.GetFileName(command.CommandName).Should().Be("dotnet-desktop-and-portable.exe");
        }
    }
}
