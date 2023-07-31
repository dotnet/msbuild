// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    [UsesVerify]
    public partial class DotnetNewInstallTests : BaseIntegrationTest
    {
        [Fact]
        public Task CannotInstallPackageAvailableFromBuiltIns()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Common.ItemTemplates::6.0.100")
                .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Fail();

            return Verify(commandResult.StdErr)
                .AddScrubber(output =>
                {
                    output.ScrubByRegex("   Microsoft\\.DotNet\\.Common\\.ItemTemplates::[A-Za-z0-9.-]+", "   Microsoft.DotNet.Common.ItemTemplates::%VERSION%");
                });
        }

        [Fact]
        public Task CanInstallPackageAvailableFromBuiltInsWithForce()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Common.ItemTemplates::6.0.100", "--force")
                .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut)
                .AddScrubber(output =>
                {
                    output.ScrubByRegex("   Microsoft.DotNet.Common.ItemTemplates::[A-Za-z0-9.-]+", "   Microsoft.DotNet.Common.ItemTemplates::%VERSION%");
                });
        }

        [Fact]
        public Task CannotInstallMultiplePackageAvailableFromBuiltIns()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Common.ItemTemplates::6.0.100", "Microsoft.DotNet.Web.ItemTemplates::5.0.0")
                .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Fail();

            return Verify(commandResult.StdErr)
                .AddScrubber(output =>
                {
                    output.ScrubByRegex("   Microsoft\\.DotNet\\.Common\\.ItemTemplates::[A-Za-z0-9.-]+", "   Microsoft.DotNet.Common.ItemTemplates::%VERSION%");
                });
        }

        [Theory]
        [InlineData("-i")]
        [InlineData("--install")]
        public Task CanShowDeprecationMessage_WhenLegacyCommandIsUsed(string commandName)
        {
            CommandResult commandResult = new DotnetNewCommand(_log, commandName, "Microsoft.DotNet.Web.ItemTemplates::5.0.0")
                .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut)
                .UseTextForParameters("common")
                .DisableRequireUniquePrefix();
        }

        [Fact]
        public Task DoNotShowDeprecationMessage_WhenNewCommandIsUsed()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Web.ItemTemplates::5.0.0")
                .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut);
        }

        [Fact]
        public Task CanShowWarning_WhenConstraintTemplateIsInstalled()
        {
            string testTemplateLocation = GetTestTemplateLocation("Constraints/RestrictedTemplate");
            CommandResult commandResult = new DotnetNewCommand(_log, "install", testTemplateLocation)
                .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut)
                .AddScrubber(output =>
                {
                    output.ScrubAndReplace(testTemplateLocation, "%TEMPLATE FOLDER%");
                    output.ScrubByRegex("dotnetcli \\(version: [A-Za-z0-9.-]+\\)", "dotnetcli (version: %VERSION%)");
                });
        }

        [Fact]
        public Task CanInstallSameSourceTwice_Folder_WhenSourceIsSpecified()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string basicFSharp = GetTestTemplateLocation("TemplateResolution/DifferentLanguagesGroup/BasicFSharp");
            new DotnetNewCommand(_log, "install", basicFSharp)
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0);

            CommandResult commandResult = new DotnetNewCommand(_log, "install", basicFSharp, "--force")
                 .WithCustomHive(home)
                 .WithWorkingDirectory(CreateTemporaryFolder())
                 .Execute();

            commandResult.Should().Pass();
            return Verify(commandResult.StdOut)
                .AddScrubber(output => output.ScrubAndReplace(basicFSharp, "%TEMPLATE FOLDER%"));
        }

        [Fact]
        public Task CanInstallSameSourceTwice_RemoteNuGet_WhenSourceIsSpecified()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string basicFSharp = GetTestTemplateLocation("TemplateResolution/DifferentLanguagesGroup/BasicFSharp");
            new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0")
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0);

            CommandResult commandResult = new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0", "--force")
                 .WithCustomHive(home)
                 .WithWorkingDirectory(CreateTemporaryFolder())
                 .Execute();

            commandResult.Should().Pass();
            return Verify(commandResult.StdOut);
        }

        [Fact]
        public Task CannotInstallSameSourceTwice_NuGet()
        {
            string home = CreateTemporaryFolder(folderName: "Home");

            new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0")
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("console")
                .And.HaveStdOutContaining("classlib");

            CommandResult commandResult = new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0")
                 .WithCustomHive(home)
                 .WithWorkingDirectory(CreateTemporaryFolder())
                 .Execute();

            commandResult.Should().Fail();
            return Verify(commandResult.StdErr);
        }

        [Fact]
        public Task CannotInstallSameSourceTwice_Folder()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string basicFSharp = GetTestTemplateLocation("TemplateResolution/DifferentLanguagesGroup/BasicFSharp");
            new DotnetNewCommand(_log, "install", basicFSharp)
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("basic");

            new DotnetNewCommand(_log, "install", basicFSharp)
                 .WithCustomHive(home)
                 .WithWorkingDirectory(CreateTemporaryFolder())
                 .Execute()
                 .Should().Fail()
                 .And.HaveStdErrContaining($"{basicFSharp} is already installed");

            CommandResult commandResult = new DotnetNewCommand(_log, "install", basicFSharp)
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            commandResult.Should().Fail();
            return Verify(commandResult.StdErr)
                .AddScrubber(output => output.ScrubAndReplace(basicFSharp, "%TEMPLATE FOLDER%"));
        }

        [Fact]
        public Task CanShowMessageInCaseShortNameConflict()
        {
            string customHivePath = CreateTemporaryFolder(folderName: "Home");
            string templateToInstall = GetTestTemplateLocation("TemplateWithConflictShortName");

            CommandResult commandResult = new DotnetNewCommand(_log, "install", templateToInstall)
                  .WithCustomHive(customHivePath)
                  .WithoutBuiltInTemplates()
                  .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut)
                .AddScrubber(output => output.ScrubAndReplace(templateToInstall, "%TEMPLATE FOLDER%"));
        }

        [Fact]
        public Task CanShowError_WhenGlobalSettingsFileIsCorrupted()
        {
            string homeDirectory = CreateTemporaryFolder();
            InstallTestTemplate("TemplateWithRequiredParameters", _log, homeDirectory);

            var globalSettingsFile = Path.Combine(homeDirectory, "packages.json");
            File.WriteAllText(globalSettingsFile, string.Empty);

            string templateToInstall = GetTestTemplateLocation("TemplateWithTags");
            CommandResult commandResult = new DotnetNewCommand(_log, "install", templateToInstall)
                .WithCustomHive(homeDirectory)
                .Execute();

            return Verify(commandResult.StdOut)
                .AddScrubber(
                output =>
                {
                    output.ScrubAndReplace(templateToInstall, "%TEMPLATE FOLDER%");
                    output.ScrubAndReplace(globalSettingsFile, "%GLOBAL SETTINGS FILE%");
                }
                );
        }
    }
}
