// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.DependencyModel;
using Xunit;

namespace Microsoft.DotNet.New.Tests
{
    public class GivenThatIWantANewApp : TestBase
    {
        [Fact]
        public void When_dotnet_new_is_invoked_mupliple_times_it_should_fail()
        {
            var rootPath = TestAssets.CreateTestDirectory().FullName;

            new NewCommand()
                .WithWorkingDirectory(rootPath)
                .Execute($"console --debug:ephemeral-hive --no-restore");

            DateTime expectedState = Directory.GetLastWriteTime(rootPath);

            var result = new NewCommand()
                .WithWorkingDirectory(rootPath)
                .ExecuteWithCapturedOutput($"console --debug:ephemeral-hive --no-restore");

            DateTime actualState = Directory.GetLastWriteTime(rootPath);

            Assert.Equal(expectedState, actualState);

            result.Should().Fail();
        }

        [Fact]
        public void RestoreDoesNotUseAnyCliProducedPackagesOnItsTemplates()
        {
            string[] cSharpTemplates = new[] { "console", "classlib", "mstest", "xunit", "web", "mvc", "webapi" };

            var rootPath = TestAssets.CreateTestDirectory().FullName;
            var packagesDirectory = Path.Combine(rootPath, "packages");

            foreach (string cSharpTemplate in cSharpTemplates)
            {
                var projectFolder = Path.Combine(rootPath, cSharpTemplate + "1");
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
            var repoRootNuGetConfig = Path.Combine(RepoDirectoriesProvider.RepoRoot, "NuGet.Config");

            new NewCommand()
                .WithWorkingDirectory(projectFolder)
                .Execute($"{projectType} --debug:ephemeral-hive --no-restore")
                .Should().Pass();

            // https://github.com/dotnet/templating/issues/946 - remove DisableImplicitAssetTargetFallback once this is fixed.
            new RestoreCommand()
                .WithWorkingDirectory(projectFolder)
                .Execute($"--configfile {repoRootNuGetConfig} --packages {packagesDirectory} /p:DisableImplicitAssetTargetFallback=true")
                .Should().Pass();
        }

        [Theory]
        [InlineData("console", "microsoft.netcore.app")]
        [InlineData("classlib", "netstandard.library")]
        public void NewProjectRestoresCorrectPackageVersion(string type, string packageName)
        {
            var rootPath = TestAssets.CreateTestDirectory(identifier: $"_{type}").FullName;
            var packagesDirectory = Path.Combine(rootPath, "packages");
            var projectName = "Project";
            var expectedVersion = "2.0.0";
            var repoRootNuGetConfig = Path.Combine(RepoDirectoriesProvider.RepoRoot, "NuGet.Config");

            new NewCommand()
                .WithWorkingDirectory(rootPath)
                .Execute($"{type} --name {projectName} -o . --debug:ephemeral-hive --no-restore")
                .Should().Pass();

            new RestoreCommand()
                .WithWorkingDirectory(rootPath)
                .Execute($"--configfile {repoRootNuGetConfig} --packages {packagesDirectory}")
                .Should().Pass();

            new DirectoryInfo(Path.Combine(packagesDirectory, packageName))
                .Should().Exist()
                .And.HaveDirectory(expectedVersion);
        }
    }
}
