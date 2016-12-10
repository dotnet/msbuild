// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Tools.Test.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using FluentAssertions;

namespace Microsoft.DotNet.New.Tests
{
    public class GivenThatIWantANewCSAppWithSpecifiedType : TestBase
    {
        [Theory]
        [InlineData("Console", false)]
        [InlineData("Lib", false)]
        [InlineData("Web", true)]
        [InlineData("Mstest", false)]
        [InlineData("XUnittest", false)]
        public void ICanRestoreBuildAndPublishTheAppWithoutWarnings(
            string projectType,
            bool useNuGetConfigForAspNet)
        {
            var rootPath = TestAssetsManager.CreateTestDirectory(callingMethod: "i").Path;

            new TestCommand("dotnet") { WorkingDirectory = rootPath }
                .Execute($"new --type {projectType}")
                .Should().Pass();

            if (useNuGetConfigForAspNet)
            {
                File.Copy("NuGet.tempaspnetpatch.config", Path.Combine(rootPath, "NuGet.Config"));
            }

            new RestoreCommand()
                .WithWorkingDirectory(rootPath)
                .Execute("/p:SkipInvalidConfigurations=true")
                .Should().Pass();

            new BuildCommand()
                .WithWorkingDirectory(rootPath)
                .ExecuteWithCapturedOutput()
                .Should().Pass()
                .And.NotHaveStdErr();

            new PublishCommand()
                .WithWorkingDirectory(rootPath)
                .ExecuteWithCapturedOutput()
                .Should().Pass()
                .And.NotHaveStdErr();
        }

        [Fact]
        public void RestoreDoesNotUseAnyCliProducedPackagesOnItsTemplates()
        {
            var cSharpTemplates = new [] { "Console", "Lib", "Web", "Mstest", "XUnittest" };

            var rootPath = TestAssetsManager.CreateTestDirectory().Path;
            var packagesDirectory = Path.Combine(rootPath, "packages");

            foreach (var cSharpTemplate in cSharpTemplates)
            {
                var projectFolder = Path.Combine(rootPath, cSharpTemplate);
                Directory.CreateDirectory(projectFolder);
                CreateAndRestoreNewProject(cSharpTemplate, projectFolder, packagesDirectory);
            }

            Directory.EnumerateFiles(packagesDirectory, $"*.nupkg", SearchOption.AllDirectories)
                .Should().NotContain(p => p.Contains("Microsoft.DotNet.Cli.Utils"));
        }

        private void CreateAndRestoreNewProject(
            string projectType,
            string projectFolder,
            string packagesDirectory)
        {
            new TestCommand("dotnet") { WorkingDirectory = projectFolder }
                .Execute($"new --type {projectType}")
                .Should().Pass();

            new RestoreCommand()
                .WithWorkingDirectory(projectFolder)
                .Execute($"--packages {packagesDirectory} /p:SkipInvalidConfigurations=true")
                .Should().Pass();
        }
    }
}
