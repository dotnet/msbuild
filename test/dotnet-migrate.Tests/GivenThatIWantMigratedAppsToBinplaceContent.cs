// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Common;
using Microsoft.DotNet.Tools.Test.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using FluentAssertions;
using System.IO;
using Microsoft.DotNet.Tools.Migrate;
using BuildCommand = Microsoft.DotNet.Tools.Test.Utilities.BuildCommand;
using System.Runtime.Loader;
using Newtonsoft.Json.Linq;

using MigrateCommand = Microsoft.DotNet.Tools.Migrate.MigrateCommand;

namespace Microsoft.DotNet.Migration.Tests
{
    public class GivenThatIWantMigratedAppsToBinplaceContent : TestBase
    {
        [Fact(Skip="Unblocking CI")]
        public void ItBinplacesContentOnBuildForConsoleApps()
        {
            var projectDirectory = TestAssets
                .GetProjectJson("TestAppWithContents")
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles()
                .WithEmptyGlobalJson()
                .Root;

            new TestCommand("dotnet")
                    .WithForwardingToConsole()
                    .Execute($"migrate {projectDirectory.FullName}")
                    .Should()
                    .Pass();

            var command = new RestoreCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should()
                .Pass();

            var result = new BuildCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput()
                .Should()
                .Pass();

            var outputDir = projectDirectory.GetDirectory("bin", "Debug", "netcoreapp1.0");
            outputDir.Should().Exist().And.HaveFile("testcontentfile.txt");
            outputDir.GetDirectory("dir").Should().Exist().And.HaveFile("mappingfile.txt");
        }

        [Fact(Skip="Unblocking CI")]
        public void ItBinplacesContentOnPublishForConsoleApps()
        {
            var projectDirectory = TestAssets
                .GetProjectJson("TestAppWithContents")
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles()
                .WithEmptyGlobalJson()
                .Root;

            new TestCommand("dotnet")
                    .WithForwardingToConsole()
                    .Execute($"migrate {projectDirectory.FullName}")
                    .Should()
                    .Pass();

            var command = new RestoreCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should()
                .Pass();

            var result = new PublishCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput()
                .Should()
                .Pass();

            var publishDir = projectDirectory.GetDirectory("bin", "Debug", "netcoreapp1.0", "publish");
            publishDir.Should().Exist().And.HaveFile("testcontentfile.txt");
            publishDir.GetDirectory("dir").Should().Exist().And.HaveFile("mappingfile.txt");
        }

        [Fact(Skip="CI does not have NPM, which is required for the publish of this app.")]
        public void ItBinplacesContentOnPublishForWebApps()
        {
            var projectDirectory = TestAssets
                .GetProjectJson("ProjectJsonWebTemplate")
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles()
                .WithEmptyGlobalJson()
                .Root;

            new TestCommand("dotnet")
                    .WithForwardingToConsole()
                    .Execute($"migrate {projectDirectory.FullName}")
                    .Should()
                    .Pass();

            var command = new RestoreCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should()
                .Pass();

            var result = new PublishCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput()
                .Should()
                .Pass();

            var publishDir = projectDirectory.GetDirectory("bin", "Debug", "netcoreapp1.0", "publish");
            publishDir.Should().Exist().And.HaveFile("README.md");
            publishDir.GetDirectory("wwwroot").Should().Exist();
        }
    }
}