// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Cli.Remove.Package.Tests
{
    public class GivenDotnetRemovePackage : SdkTest
    {
        private Func<string, string> HelpText = (defaultVal) => $@"Description:
  Remove a NuGet package reference from the project.

Usage:
  dotnet remove <PROJECT> package <PACKAGE_NAME> [options]

Arguments:
  <PROJECT>         The project file to operate on. If a file is not specified, the command will search the current directory for one. [default: {PathUtility.EnsureTrailingSlash(defaultVal)}]
  <PACKAGE_NAME>    The package reference to remove.

Options:
  --interactive     Allows the command to stop and wait for user input or action (for example to complete authentication).
  -?, -h, --help    Show command line help.";

        private Func<string, string> RemoveCommandHelpText = (defaultVal) => $@"Description:
  .NET Remove Command

Usage:
  dotnet remove <PROJECT> [command] [options]

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
