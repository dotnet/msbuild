// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;

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
