// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.Remove.Package.Tests
{
    public class GivenDotnetRemovePackage : SdkTest
    {
        private const string HelpText = @"Usage: dotnet remove <PROJECT> package [options] <PACKAGE_NAME>

Arguments:
  <PROJECT>        The project file to operate on. If a file is not specified, the command will search the current directory for one.
  <PACKAGE_NAME>   The package reference to remove.

Options:
  -h, --help      Show command line help.
  --interactive   Allows the command to stop and wait for user input or action (for example to complete authentication).";

        private const string RemoveCommandHelpText = @"Usage: dotnet remove [options] <PROJECT> [command]

Arguments:
  <PROJECT>   The project file to operate on. If a file is not specified, the command will search the current directory for one.

Options:
  -h, --help   Show command line help.

Commands:
  package <PACKAGE_NAME>     Remove a NuGet package reference from the project.
  reference <PROJECT_PATH>   Remove a project-to-project reference from the project.";

        public GivenDotnetRemovePackage(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        public void WhenHelpOptionIsPassedItPrintsUsage(string helpArg)
        {
            var cmd = new DotnetCommand(Log).Execute($"remove", "package", helpArg);
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText);
        }

        [Theory]
        [InlineData("")]
        [InlineData("unknownCommandName")]
        public void WhenNoCommandIsPassedItPrintsError(string commandName)
        {
            var cmd = new DotnetCommand(Log)
                .Execute("remove", commandName);
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(CommonLocalizableStrings.RequiredCommandNotPassed);
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(RemoveCommandHelpText);
        }

        [Fact]
        public void WhenReferencedPackageIsPassedItGetsRemoved()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppSimple")
                .WithSource().Path;

            var packageName = "Newtonsoft.Json";
            var add = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("add", "package", packageName);
            add.Should().Pass();
          

            var remove = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"remove", "package", packageName);

            remove.Should().Pass();
            remove.StdOut.Should().Contain($"Removing PackageReference for package '{packageName}' from project '{projectDirectory + Path.DirectorySeparatorChar}TestAppSimple.csproj'.");
            remove.StdErr.Should().BeEmpty();
        }
    }
}
