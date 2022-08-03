// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.TemplateEngine.TestHelper;
using VerifyXunit;
using Xunit;

namespace Microsoft.DotNet.New.Tests
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
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
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
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut).UniqueForOSPlatform();
        }

        [Fact]
        public Task Constraints_CanShowMessageIfTemplateGroupIsRestricted()
        {
            var customHivePath = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate("Constraints/RestrictedTemplate", _log, customHivePath);
            Helpers.InstallTestTemplate("TemplateWithSourceName", _log, customHivePath);

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
            var customHivePath = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate("Constraints/RestrictedTemplate", _log, customHivePath);
            Helpers.InstallTestTemplate("TemplateWithSourceName", _log, customHivePath);

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
