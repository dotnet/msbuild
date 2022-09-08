// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using FluentAssertions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public partial class DotnetNewList
    {
        [Theory]
        [InlineData("-l")]
        [InlineData("--list")]
        public Task BasicTest_WhenLegacyCommandIsUsed(string commandName)
        {
            var commandResult = new DotnetNewCommand(_log, commandName)
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
            var commandResult = new DotnetNewCommand(_log, "list")
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
            var customHivePath = CreateTemporaryFolder(folderName: "Home");
            InstallTestTemplate("Constraints/RestrictedTemplate", _log, customHivePath);
            InstallTestTemplate("TemplateWithSourceName", _log, customHivePath);

            var commandResult = new DotnetNewCommand(_log, "list", "RestrictedTemplate")
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
            var customHivePath = CreateTemporaryFolder(folderName: "Home");
            InstallTestTemplate("Constraints/RestrictedTemplate", _log, customHivePath);
            InstallTestTemplate("TemplateWithSourceName", _log, customHivePath);

            var commandResult = new DotnetNewCommand(_log, "list", "RestrictedTemplate", "--ignore-constraints")
                  .WithCustomHive(customHivePath)
                  .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut);
        }
    }
}
