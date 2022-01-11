// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Common;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.Remove.Package.Tests
{
    public class GivenDotnetRemovePackage : SdkTest
    {
        private Func<string, string> HelpText = (defaultVal) => $@"Description:
  Remove a NuGet package reference from the project.

Usage:
  dotnet [options] remove <PROJECT> package <PACKAGE_NAME>

Arguments:
  <PROJECT>         The project file to operate on. If a file is not specified, the command will search the current directory for one. [default: {PathUtility.EnsureTrailingSlash(defaultVal)}]
  <PACKAGE_NAME>    The package reference to remove.

Options:
  --interactive     Allows the command to stop and wait for user input or action (for example to complete authentication).
  -?, -h, --help    Show command line help.";

        private Func<string, string> RemoveCommandHelpText = (defaultVal) => $@"Description:
  .NET Remove Command

Usage:
  dotnet [options] remove <PROJECT> [command]

Arguments:
  <PROJECT>    The project file to operate on. If a file is not specified, the command will search the current directory for one. [default: {PathUtility.EnsureTrailingSlash(defaultVal)}]

Options:
  -?, -h, --help    Show command line help.

Commands:
  package <PACKAGE_NAME>      Remove a NuGet package reference from the project.
  reference <PROJECT_PATH>    Remove a project-to-project reference from the project";

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
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText(Directory.GetCurrentDirectory()));
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
