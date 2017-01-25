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
            zip.Entries.Should().Contain(e => e.FullName == "PJAppWithDeprecatedPackOptions.nuspec");

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
    }
}
