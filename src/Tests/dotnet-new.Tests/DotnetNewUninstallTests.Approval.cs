// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;

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
