// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    [UsesVerify]
    public partial class DotnetNewUpdateTests
    {
        [Fact]
        public Task CanShowMessageInCaseShortNameConflict()
        {
            string customHivePath = CreateTemporaryFolder(folderName: "Home");
            InstallTestTemplate("TemplateWithConflictShortName", _log, customHivePath);

            CommandResult commandResult = new DotnetNewCommand(_log, "update")
                  .WithCustomHive(customHivePath)
                  .WithoutBuiltInTemplates()
                  .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut);
        }

        [Fact]
        public Task CanShowError_WhenGlobalSettingsFileIsCorrupted()
        {
            string homeDirectory = CreateTemporaryFolder();
            new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0")
                .WithCustomHive(homeDirectory)
                .WithoutBuiltInTemplates()
                .Execute()
                .Should()
                .Pass()
                .And.HaveStdOutContaining("console");

            var globalSettingsFile = Path.Combine(homeDirectory, "packages.json");
            File.WriteAllText(globalSettingsFile, string.Empty);

            CommandResult commandResult = new DotnetNewCommand(_log, "update")
                .WithCustomHive(homeDirectory)
                .WithoutBuiltInTemplates()
                .Execute();

            return Verify(commandResult.StdOut)
                .AddScrubber(output => output.ScrubAndReplace(globalSettingsFile, "%GLOBAL SETTINGS FILE%"))
                .AddScrubber(output =>
                {
                    output.UnixifyNewlines()
                          .ScrubAndReplace("All template packages are up-to-date.", string.Empty);

                    output.ScrubAndReplace("\n", string.Empty);
                });
        }
    }
}
