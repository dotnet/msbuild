// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    [UsesVerify]
    [Collection("Verify Tests")]
    public class DotnetNewTests : BaseIntegrationTest, IClassFixture<SharedHomeDirectory>
    {
        private readonly SharedHomeDirectory _sharedHome;
        private readonly ITestOutputHelper _log;

        public DotnetNewTests(SharedHomeDirectory sharedHome, ITestOutputHelper log) : base(log)
        {
            _log = log;
            _sharedHome = sharedHome;
        }

        [Fact]
        public Task CanShowBasicInfo()
        {
            CommandResult commandResult = new DotnetNewCommand(_log)
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0).And.NotHaveStdErr();

            return Verify(commandResult.StdOut).UniqueForOSPlatform();
        }

        [Theory]
        [InlineData("-v", "q")]
        [InlineData("-v", "quiet")]
        [InlineData("--verbosity", "q")]
        [InlineData("--verbosity", "quiet")]
        public void CanUseQuietMode(string optionName, string optionValue)
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "search", "template-does-not-exist", optionName, optionValue)
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(103)
                .And.NotHaveStdErr()
                .And.NotHaveStdOut();
        }

        [Fact]
        public void CanUseQuietMode_ViaEnvVar()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "search", "template-does-not-exist")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .WithEnvironmentVariable("DOTNET_CLI_CONTEXT_OUTPUT", "false")
                .WithEnvironmentVariable("DOTNET_CLI_CONTEXT_ERROR", "false")
                .Execute();

            commandResult.Should()
                .ExitWith(103)
                .And.NotHaveStdErr()
                .And.NotHaveStdOut();
        }

        [Theory]
        [InlineData("-v", "m")]
        [InlineData("-v", "minimal")]
        [InlineData("--verbosity", "m")]
        [InlineData("--verbosity", "minimal")]
        public Task CanUseMinimalMode(string optionName, string optionValue)
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "search", "template-does-not-exist", optionName, optionValue)
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                 .ExitWith(103)
                 .And.NotHaveStdOut();

            return Verify(commandResult.StdErr)
                .UseTextForParameters("common")
                .DisableRequireUniquePrefix();
        }

        [Theory]
        [InlineData("-v", "n")]
        [InlineData("-v", "normal")]
        [InlineData("--verbosity", "n")]
        [InlineData("--verbosity", "normal")]
        public Task CanUseNormalMode(string optionName, string optionValue)
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "search", "template-does-not-exist", optionName, optionValue)
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                 .ExitWith(103);

            return Verify(commandResult.FormatOutputStreams())
                .UseTextForParameters("common")
                .DisableRequireUniquePrefix();
        }

        [Theory]
        [InlineData("-v", "diag")]
        [InlineData("-v", "diagnostic")]
        [InlineData("--verbosity", "diag")]
        [InlineData("--verbosity", "diagnostic")]
        [InlineData("--diagnostics", null)]
        [InlineData("-d", null)]
        public void CanUseDiagMode(string optionName, string? optionValue)
        {
            CommandResult commandResult = new DotnetNewCommand(
                _log,
                string.IsNullOrEmpty(optionValue)
                    ? new[] { "search", "template-does-not-exist", optionName }
                    : new[] { "search", "template-does-not-exist", optionName, optionValue })
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                 .ExitWith(103)
                 .And.HaveStdOutContaining("[Debug] [Template Engine] => [Execute]: Execute started");
        }
    }
}
