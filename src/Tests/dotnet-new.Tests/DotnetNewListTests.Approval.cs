// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public partial class DotnetNewListTests
    {
        [Theory]
        [InlineData("-l")]
        [InlineData("--list")]
        public Task BasicTest_WhenLegacyCommandIsUsed(string commandName)
        {
            CommandResult commandResult = new DotnetNewCommand(_log, commandName)
                .WithCustomHive(_sharedHome.HomeDirectory)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut)
                .UniqueForOSPlatform()
                .UseTextForParameters("common")
                .DisableRequireUniquePrefix();
        }

        [Fact]
        public Task BasicTest_WhenListCommandIsUsed()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "list")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut).UniqueForOSPlatform();
        }

        [Fact]
        public Task Constraints_CanShowMessageIfTemplateGroupIsRestricted()
        {
            string customHivePath = CreateTemporaryFolder(folderName: "Home");
            InstallTestTemplate("Constraints/RestrictedTemplate", _log, customHivePath);
            InstallTestTemplate("TemplateWithSourceName", _log, customHivePath);

            CommandResult commandResult = new DotnetNewCommand(_log, "list", "RestrictedTemplate")
                  .WithCustomHive(customHivePath)
                  .Execute();

            commandResult
                .Should()
                .Fail();

            return Verify(commandResult.StdErr);
        }

        [Fact]
        public Task Constraints_CanIgnoreConstraints()
        {
            string customHivePath = CreateTemporaryFolder(folderName: "Home");
            InstallTestTemplate("Constraints/RestrictedTemplate", _log, customHivePath);
            InstallTestTemplate("TemplateWithSourceName", _log, customHivePath);

            CommandResult commandResult = new DotnetNewCommand(_log, "list", "RestrictedTemplate", "--ignore-constraints")
                  .WithCustomHive(customHivePath)
                  .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut);
        }

        [Fact]
        public Task CanShowMessageInCaseShortNameConflict()
        {
            string customHivePath = CreateTemporaryFolder(folderName: "Home");
            InstallTestTemplate("TemplateWithConflictShortName", _log, customHivePath);

            CommandResult commandResult = new DotnetNewCommand(_log, "list")
                  .WithCustomHive(customHivePath)
                  .WithoutBuiltInTemplates()
                  .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut);
        }
    }
}
