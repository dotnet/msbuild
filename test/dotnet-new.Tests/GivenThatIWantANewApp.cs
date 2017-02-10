// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.New.Tests
{
    public class GivenThatIWantANewApp : TestBase
    {
        [Fact]
        public void When_dotnet_new_is_invoked_mupliple_times_it_should_fail()
        {
            var rootPath = TestAssetsManager.CreateTestDirectory().Path;

            new NewCommand()
                .WithWorkingDirectory(rootPath)
                .Execute($"console --debug:ephemeral-hive");

            DateTime expectedState = Directory.GetLastWriteTime(rootPath);

            var result = new NewCommand()
                .WithWorkingDirectory(rootPath)
                .ExecuteWithCapturedOutput($"console --debug:ephemeral-hive");

            DateTime actualState = Directory.GetLastWriteTime(rootPath);

            Assert.Equal(expectedState, actualState);

            result.Should().Fail();
        }

        [Fact]
        public void RestoreDoesNotUseAnyCliProducedPackagesOnItsTemplates()
        {
            string[] cSharpTemplates = new[] { "console", "classlib", "mstest", "xunit", "web", "mvc", "webapi" };

            var rootPath = TestAssetsManager.CreateTestDirectory().Path;
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
            new NewCommand()
                .WithWorkingDirectory(projectFolder)
                .Execute($"{projectType} --debug:ephemeral-hive")
                .Should().Pass();

            new RestoreCommand()
                .WithWorkingDirectory(projectFolder)
                .Execute($"--packages {packagesDirectory}")
                .Should().Pass();
        }

        [Fact]
        public void NewClassLibRestoresCorrectNetStandardLibraryVersion()
        {
            var rootPath = TestAssetsManager.CreateTestDirectory().Path;
            var packagesDirectory = Path.Combine(rootPath, "packages");
            var projectName = "Library";
            var projectFileName = $"{projectName}.csproj";

            new NewCommand()
                .WithWorkingDirectory(rootPath)
                .Execute($"classlib --name {projectName} -o .")
                .Should().Pass();

            new RestoreCommand()
                .WithWorkingDirectory(rootPath)
                .Execute($"--packages {packagesDirectory}")
                .Should().Pass();

            var expectedVersion = XDocument.Load(Path.Combine(rootPath, projectFileName))
                .Elements("Project")
                .Elements("PropertyGroup")
                .Elements("NetStandardImplicitPackageVersion")
                .FirstOrDefault()
                ?.Value;

            expectedVersion.Should().NotBeNullOrEmpty("Could not find NetStandardImplicitPackageVersion property in a new classlib.");

            new DirectoryInfo(Path.Combine(packagesDirectory, "netstandard.library"))
                .Should().Exist()
                .And.HaveDirectory(expectedVersion);
        }
    }
}
