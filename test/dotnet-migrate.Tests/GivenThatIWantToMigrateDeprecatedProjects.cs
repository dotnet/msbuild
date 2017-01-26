// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Microsoft.DotNet.Migration.Tests
{
    public class GivenThatIWantToMigrateDeprecatedProjects : TestBase
    {
        [Fact]
        public void WhenMigratingAProjectWithDeprecatedPackOptionsWarningsArePrinted()
        {
            var projectDirectory = TestAssets
                .GetProjectJson(TestAssetKinds.NonRestoredTestProjects, "PJAppWithDeprecatedPackOptions")
                .CreateInstance()
                .WithSourceFiles()
                .Root;

            var cmd = new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .ExecuteWithCapturedOutput("migrate");

            cmd.Should().Pass();

            cmd.StdOut.Should().Contain(
                "The 'repository' option in the root is deprecated. Use it in 'packOptions' instead.");
            cmd.StdOut.Should().Contain(
                "The 'projectUrl' option in the root is deprecated. Use it in 'packOptions' instead.");
            cmd.StdOut.Should().Contain(
                "The 'licenseUrl' option in the root is deprecated. Use it in 'packOptions' instead.");
            cmd.StdOut.Should().Contain(
                "The 'iconUrl' option in the root is deprecated. Use it in 'packOptions' instead.");
            cmd.StdOut.Should().Contain(
                "The 'owners' option in the root is deprecated. Use it in 'packOptions' instead.");
            cmd.StdOut.Should().Contain(
                "The 'tags' option in the root is deprecated. Use it in 'packOptions' instead.");
            cmd.StdOut.Should().Contain(
                "The 'releaseNotes' option in the root is deprecated. Use it in 'packOptions' instead.");
            cmd.StdOut.Should().Contain(
                "The 'requireLicenseAcceptance' option in the root is deprecated. Use it in 'packOptions' instead.");
            cmd.StdOut.Should().Contain(
                "The 'summary' option in the root is deprecated. Use it in 'packOptions' instead.");
            cmd.StdOut.Should().Contain(
                "The 'packInclude' option is deprecated. Use 'files' in 'packOptions' instead.");
        }

        [Fact]
        public void WhenMigratingAProjectWithDeprecatedPackOptionsItSucceeds()
        {
            var projectDirectory = TestAssets
                .GetProjectJson(TestAssetKinds.NonRestoredTestProjects, "PJAppWithDeprecatedPackOptions")
                .CreateInstance()
                .WithSourceFiles()
                .Root;

            new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .Execute("migrate")
                 .Should().Pass();

            new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .Execute("restore")
                 .Should().Pass();

            new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .Execute("build")
                 .Should().Pass();

            new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .Execute("pack")
                 .Should().Pass();

            var outputDir = projectDirectory.GetDirectory("bin", "Debug");
            outputDir.Should().Exist()
                .And.HaveFile("PJAppWithDeprecatedPackOptions.1.0.0.nupkg");

            var outputPackage = outputDir.GetFile("PJAppWithDeprecatedPackOptions.1.0.0.nupkg");

            var zip = ZipFile.Open(outputPackage.FullName, ZipArchiveMode.Read);
            zip.Entries.Should().Contain(e => e.FullName == "PJAppWithDeprecatedPackOptions.nuspec")
                .And.Contain(e => e.FullName == "content/Content1.txt")
                .And.Contain(e => e.FullName == "content/Content2.txt");

            var manifestReader = new StreamReader(
                zip.Entries.First(e => e.FullName == "PJAppWithDeprecatedPackOptions.nuspec").Open());

            // NOTE: Commented out those that are not migrated.
            // https://microsoft.sharepoint.com/teams/netfx/corefx/_layouts/15/WopiFrame.aspx?sourcedoc=%7B0cfbc196-0645-4781-84c6-5dffabd76bee%7D&action=edit&wd=target%28Planning%2FMSBuild%20CLI%20integration%2Eone%7C41D470DD-CF44-4595-8E05-0CE238864B55%2FProject%2Ejson%20Migration%7CA553D979-EBC6-484B-A12E-036E0730864A%2F%29
            var nuspecXml = XDocument.Parse(manifestReader.ReadToEnd());
            nuspecXml.Descendants().Single(e => e.Name.LocalName == "projectUrl").Value
                .Should().Be("http://projecturl/");
            nuspecXml.Descendants().Single(e => e.Name.LocalName == "licenseUrl").Value
                .Should().Be("http://licenseurl/");
            nuspecXml.Descendants().Single(e => e.Name.LocalName == "iconUrl").Value
                .Should().Be("http://iconurl/");
            //nuspecXml.Descendants().Single(e => e.Name.LocalName == "owners").Value
            //    .Should().Be("owner1,owner2");
            nuspecXml.Descendants().Single(e => e.Name.LocalName == "tags").Value
                .Should().Be("tag1 tag2");
            nuspecXml.Descendants().Single(e => e.Name.LocalName == "releaseNotes").Value
                .Should().Be("releaseNotes");
            nuspecXml.Descendants().Single(e => e.Name.LocalName == "requireLicenseAcceptance").Value
                .Should().Be("true");
            //nuspecXml.Descendants().Single(e => e.Name.LocalName == "summary").Value
            //    .Should().Be("summary");

            var repositoryNode = nuspecXml.Descendants().Single(e => e.Name.LocalName == "repository");
            repositoryNode.Attributes("type").Single().Value.Should().Be("git");
            repositoryNode.Attributes("url").Single().Value.Should().Be("http://url/");
        }

        [Fact]
        public void WhenMigratingAProjectWithDeprecatedCompilationOptionsWarningsArePrinted()
        {
            var projectDirectory = TestAssets
                .GetProjectJson(TestAssetKinds.NonRestoredTestProjects, "PJAppWithDeprecatedCompilationOptions")
                .CreateInstance()
                .WithSourceFiles()
                .Root;

            var cmd = new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .ExecuteWithCapturedOutput("migrate");

            cmd.Should().Pass();

            cmd.StdOut.Should().Contain(
                "The 'compilerName' option in the root is deprecated. Use it in 'buildOptions' instead.");
            cmd.StdOut.Should().Contain(
                "The 'compilationOptions' option is deprecated. Use 'buildOptions' instead.");
        }

        [Fact]
        public void WhenMigratingAProjectWithDeprecatedCompilationOptionsItSucceeds()
        {
            var projectDirectory = TestAssets
                .GetProjectJson(TestAssetKinds.NonRestoredTestProjects, "PJAppWithDeprecatedCompilationOptions")
                .CreateInstance()
                .WithSourceFiles()
                .Root;

            new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .Execute("migrate")
                 .Should().Pass();

            new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .Execute("restore")
                 .Should().Pass();

            new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .Execute("build")
                 .Should().Pass();
        }

        [Fact]
        public void WhenMigratingAProjectWithDeprecatedContentOptionsWarningsArePrinted()
        {
            var projectDirectory = TestAssets
                .GetProjectJson(TestAssetKinds.NonRestoredTestProjects, "PJAppWithDeprecatedContentOptions")
                .CreateInstance()
                .WithSourceFiles()
                .Root;

            var cmd = new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .ExecuteWithCapturedOutput("migrate");

            cmd.Should().Pass();

            cmd.StdOut.Should().Contain(
                "The 'content' option is deprecated. Use 'publishOptions' to publish or 'copyToOutput' in 'buildOptions' to copy to build output instead.");
            cmd.StdOut.Should().Contain(
                "The 'contentExclude' option is deprecated. Use 'publishOptions' to publish or 'copyToOutput' in 'buildOptions' to copy to build output instead.");
            cmd.StdOut.Should().Contain(
                "The 'contentFiles' option is deprecated. Use 'publishOptions' to publish or 'copyToOutput' in 'buildOptions' to copy to build output instead.");
            cmd.StdOut.Should().Contain(
                "The 'contentBuiltIn' option is deprecated. Use 'publishOptions' to publish or 'copyToOutput' in 'buildOptions' to copy to build output instead.");
        }

        [Fact]
        public void WhenMigratingAProjectWithDeprecatedContentOptionsItSucceeds()
        {
            var projectDirectory = TestAssets
                .GetProjectJson(TestAssetKinds.NonRestoredTestProjects, "PJAppWithDeprecatedContentOptions")
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .GetDirectory("project");

            new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .Execute("migrate")
                 .Should().Pass();

            new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .Execute("restore")
                 .Should().Pass();

            new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .Execute("build")
                 .Should().Pass();

            new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .Execute("publish")
                 .Should().Pass();

            var outputDir = projectDirectory.GetDirectory("bin", "Debug", "netcoreapp1.0");
            outputDir.Should().Exist()
                .And.HaveFiles(new[]
                    {
                        "ContentFile1.txt",
                        "ContentFile2.txt",
                        "ContentFileBuiltIn1.txt",
                        "ContentFileBuiltIn2.txt",
                        "IncludeThis.txt",
                    });
            Directory.Exists(Path.Combine(outputDir.FullName, "ExcludeThis1.txt")).Should().BeFalse();
            Directory.Exists(Path.Combine(outputDir.FullName, "ExcludeThis2.txt")).Should().BeFalse();

            var publishDir = projectDirectory.GetDirectory("bin", "Debug", "netcoreapp1.0", "publish");
            publishDir.Should().Exist()
                .And.HaveFiles(new[]
                    {
                        "ContentFile1.txt",
                        "ContentFile2.txt",
                        "ContentFileBuiltIn1.txt",
                        "ContentFileBuiltIn2.txt",
                        "IncludeThis.txt",
                    });
            Directory.Exists(Path.Combine(publishDir.FullName, "ExcludeThis1.txt")).Should().BeFalse();
            Directory.Exists(Path.Combine(publishDir.FullName, "ExcludeThis2.txt")).Should().BeFalse();
        }

        [Fact]
        public void WhenMigratingAProjectWithDeprecatedCompileOptionsWarningsArePrinted()
        {
            var projectDirectory = TestAssets
                .GetProjectJson(TestAssetKinds.NonRestoredTestProjects, "PJAppWithDeprecatedCompileOptions")
                .CreateInstance()
                .WithSourceFiles()
                .Root;

            var cmd = new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .ExecuteWithCapturedOutput("migrate");

            cmd.Should().Pass();

            cmd.StdOut.Should().Contain(
                "The 'compile' option is deprecated. Use 'compile' in 'buildOptions' instead.");
            cmd.StdOut.Should().Contain(
                "The 'compileFiles' option is deprecated. Use 'compile' in 'buildOptions' instead.");
        }

        [Fact]
        public void WhenMigratingAProjectWithDeprecatedCompileOptionsItSucceeds()
        {
            var projectDirectory = TestAssets
                .GetProjectJson(TestAssetKinds.NonRestoredTestProjects, "PJAppWithDeprecatedCompileOptions")
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .GetDirectory("project");

            new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .Execute("migrate")
                 .Should().Pass();

            new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .Execute("restore")
                 .Should().Pass();

            new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .Execute("build")
                 .Should().Pass();
        }

        [Fact]
        public void WhenMigratingAProjectWithDeprecatedCompileBuiltInOptionsWarningsArePrinted()
        {
            var projectDirectory = TestAssets
                .GetProjectJson(TestAssetKinds.NonRestoredTestProjects, "PJAppWithDeprecatedCompileBuiltInOptions")
                .CreateInstance()
                .WithSourceFiles()
                .Root;

            var cmd = new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .ExecuteWithCapturedOutput("migrate");

            cmd.Should().Pass();

            cmd.StdOut.Should().Contain(
                "The 'compileBuiltIn' option is deprecated. Use 'compile' in 'buildOptions' instead.");
        }

        [Fact]
        public void WhenMigratingAProjectWithDeprecatedCompileBuiltInOptionsItSucceeds()
        {
            var projectDirectory = TestAssets
                .GetProjectJson(TestAssetKinds.NonRestoredTestProjects, "PJAppWithDeprecatedCompileBuiltInOptions")
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .GetDirectory("project");

            new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .Execute("migrate")
                 .Should().Pass();

            new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .Execute("restore")
                 .Should().Pass();

            //Issue: https://github.com/dotnet/cli/issues/5467
            //new DotnetCommand()
            //     .WithWorkingDirectory(projectDirectory)
            //     .Execute("build")
            //     .Should().Pass();
        }

        [Fact]
        public void WhenMigratingAProjectWithDeprecatedCompileExcludeOptionsWarningsArePrinted()
        {
            var projectDirectory = TestAssets
                .GetProjectJson(TestAssetKinds.NonRestoredTestProjects, "PJAppWithDeprecatedCompileExcludeOptions")
                .CreateInstance()
                .WithSourceFiles()
                .Root;

            var cmd = new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .ExecuteWithCapturedOutput("migrate");

            cmd.Should().Pass();

            cmd.StdOut.Should().Contain(
                "The 'compileExclude' option is deprecated. Use 'compile' in 'buildOptions' instead.");
        }

        [Fact]
        public void WhenMigratingAProjectWithDeprecatedCompileExcludeOptionsItSucceeds()
        {
            var projectDirectory = TestAssets
                .GetProjectJson(TestAssetKinds.NonRestoredTestProjects, "PJAppWithDeprecatedCompileExcludeOptions")
                .CreateInstance()
                .WithSourceFiles()
                .Root;

            new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .Execute("migrate")
                 .Should().Pass();

            new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .Execute("restore")
                 .Should().Pass();

            // Issue: https://github.com/dotnet/cli/issues/5461
            //new DotnetCommand()
            //     .WithWorkingDirectory(projectDirectory)
            //     .Execute("build")
            //     .Should().Pass();
        }

        [Fact]
        public void WhenMigratingAProjectWithDeprecatedResourceOptionsWarningsArePrinted()
        {
            var projectDirectory = TestAssets
                .GetProjectJson(TestAssetKinds.NonRestoredTestProjects, "PJAppWithDeprecatedResourceOptions")
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .GetDirectory("project");

            var cmd = new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .ExecuteWithCapturedOutput("migrate");

            cmd.Should().Pass();

            cmd.StdOut.Should().Contain(
                "The 'resource' option is deprecated. Use 'embed' in 'buildOptions' instead.");
            cmd.StdOut.Should().Contain(
                "The 'resourceFiles' option is deprecated. Use 'embed' in 'buildOptions' instead.");
        }

        [Fact]
        public void WhenMigratingAProjectWithDeprecatedResourceOptionsItSucceeds()
        {
            var projectDirectory = TestAssets
                .GetProjectJson(TestAssetKinds.NonRestoredTestProjects, "PJAppWithDeprecatedResourceOptions")
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .GetDirectory("project");

            new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .Execute("migrate")
                 .Should().Pass();

            new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .Execute("restore")
                 .Should().Pass();

            new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .Execute("build")
                 .Should().Pass();

            var cmd = new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .ExecuteWithCapturedOutput("run");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("3 Resources Found:");
        }

        [Fact]
        public void WhenMigratingAProjectWithDeprecatedResourceBuiltInOptionsWarningsArePrinted()
        {
            var projectDirectory = TestAssets
                .GetProjectJson(TestAssetKinds.NonRestoredTestProjects, "PJAppWithDeprecatedResourceBuiltInOptions")
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .GetDirectory("project");

            var cmd = new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .ExecuteWithCapturedOutput("migrate");

            cmd.Should().Pass();

            cmd.StdOut.Should().Contain(
                "The 'resourceBuiltIn' option is deprecated. Use 'embed' in 'buildOptions' instead.");
        }

        [Fact]
        public void WhenMigratingAProjectWithDeprecatedResourceBuiltInOptionsItSucceeds()
        {
            var projectDirectory = TestAssets
                .GetProjectJson(TestAssetKinds.NonRestoredTestProjects, "PJAppWithDeprecatedResourceBuiltInOptions")
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .GetDirectory("project");

            new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .Execute("migrate")
                 .Should().Pass();

            new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .Execute("restore")
                 .Should().Pass();

            new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .Execute("build")
                 .Should().Pass();

            var cmd = new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .ExecuteWithCapturedOutput("run");
            cmd.Should().Pass();
            // Issue: https://github.com/dotnet/cli/issues/5467
            //cmd.StdOut.Should().Contain("2 Resources Found:");
        }

        [Fact]
        public void WhenMigratingAProjectWithDeprecatedResourceExcludeOptionsWarningsArePrinted()
        {
            var projectDirectory = TestAssets
                .GetProjectJson(TestAssetKinds.NonRestoredTestProjects, "PJAppWithDeprecatedResourceExcludeOptions")
                .CreateInstance()
                .WithSourceFiles()
                .Root;

            var cmd = new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .ExecuteWithCapturedOutput("migrate");

            cmd.Should().Pass();

            cmd.StdOut.Should().Contain(
                "The 'resourceExclude' option is deprecated. Use 'embed' in 'buildOptions' instead.");
        }

        [Fact]
        public void WhenMigratingAProjectWithDeprecatedResourceExcludeOptionsItSucceeds()
        {
            var projectDirectory = TestAssets
                .GetProjectJson(TestAssetKinds.NonRestoredTestProjects, "PJAppWithDeprecatedResourceExcludeOptions")
                .CreateInstance()
                .WithSourceFiles()
                .Root;

            new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .Execute("migrate")
                 .Should().Pass();

            new DotnetCommand()
                 .WithWorkingDirectory(projectDirectory)
                 .Execute("restore")
                 .Should().Pass();

            // Issue: https://github.com/dotnet/cli/issues/5461
            //new DotnetCommand()
            //     .WithWorkingDirectory(projectDirectory)
            //     .Execute("build")
            //     .Should().Pass();

            //var cmd = new DotnetCommand()
            //     .WithWorkingDirectory(projectDirectory)
            //     .ExecuteWithCapturedOutput("run");
            //cmd.Should().Pass();
            //cmd.StdOut.Should().Contain("0 Resources Found:");
        }
    }
}
