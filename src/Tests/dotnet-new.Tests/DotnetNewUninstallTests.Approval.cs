// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    [UsesVerify]
    public partial class DotnetNewUninstallTests
    {
        [Fact]
        public Task CanShowMessageInCaseShortNameConflict()
        {
            string customHivePath = CreateTemporaryFolder(folderName: "Home");
            string templateLocation = InstallTestTemplate("TemplateWithConflictShortName", _log, customHivePath);

            CommandResult commandResult = new DotnetNewCommand(_log, "uninstall")
                  .WithCustomHive(customHivePath)
                  .WithoutBuiltInTemplates()
                  .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut)
                .AddScrubber(output => output.ScrubAndReplace(templateLocation, "%TEMPLATE FOLDER%"));
        }

        [Fact]
        public Task CanShowError_WhenGlobalSettingsFileIsCorrupted()
        {
            string homeDirectory = CreateTemporaryFolder();
            InstallTestTemplate("TemplateWithRequiredParameters", _log, homeDirectory);

            var globalSettingsFile = Path.Combine(homeDirectory, "packages.json");
            File.WriteAllText(globalSettingsFile, string.Empty);

            CommandResult commandResult = new DotnetNewCommand(_log, "uninstall", "TemplateWithRequiredParameters")
                .WithCustomHive(homeDirectory)
                .Execute();

            return Verify(commandResult.StdOut)
                .AddScrubber(output => output.ScrubAndReplace(globalSettingsFile, "%GLOBAL SETTINGS FILE%"));
        }
    }
}
