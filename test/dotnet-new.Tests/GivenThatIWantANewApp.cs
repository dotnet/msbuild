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
        [InlineData("console", "RuntimeFrameworkVersion", "microsoft.netcore.app")]
        [InlineData("classlib", "NetStandardImplicitPackageVersion", "netstandard.library")]
        public void NewProjectRestoresCorrectPackageVersion(string type, string propertyName, string packageName)
        {
            var rootPath = TestAssets.CreateTestDirectory(identifier: $"_{type}").FullName;
            var packagesDirectory = Path.Combine(rootPath, "packages");
            var projectName = "Project";
            var expectedVersion = GetFrameworkPackageVersion();
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

            string GetFrameworkPackageVersion()
            {
                var dotnetDir = new FileInfo(DotnetUnderTest.FullName).Directory;
                var sharedFxDir = dotnetDir
                    .GetDirectory("shared", "Microsoft.NETCore.App")
                    .EnumerateDirectories()
                    .Single(d => d.Name.StartsWith("2.1.0"));

                if (packageName == "microsoft.netcore.app")
                {
                    return sharedFxDir.Name;
                }

                var depsFile = Path.Combine(sharedFxDir.FullName, "Microsoft.NETCore.App.deps.json");
                using (var stream = File.OpenRead(depsFile))
                using (var reader = new DependencyContextJsonReader())
                {
                    var context = reader.Read(stream);
                    var dependency = context
                        .RuntimeLibraries
                        .Single(library => string.Equals(library.Name, packageName, StringComparison.OrdinalIgnoreCase));

                    return dependency.Version;
                }
            }
        }
    }
}
