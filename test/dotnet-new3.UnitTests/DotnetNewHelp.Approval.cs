// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ApprovalTests;
using FluentAssertions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;
using Xunit.Abstractions;

namespace Dotnet_new3.IntegrationTests
{
    public partial class DotnetNewHelp
    {
        [Theory]
        [InlineData("-h")]
        [InlineData("/h")]
        [InlineData("--help")]
        [InlineData("-?")]
        [InlineData("/?")]
        public void CanShowHelp(string command)
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            var commandResult = new DotnetNewCommand(_log, command)
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult.Should().ExitWith(0)
                .And.NotHaveStdErr();

            Approvals.Verify(commandResult.StdOut);
        }

        [Theory]
        [InlineData("-h")]
        [InlineData("--help")]
        public void CanShowHelp_Install(string option)
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            var commandResult = new DotnetNewCommand(_log, "install", option)
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult.Should().ExitWith(0)
                .And.NotHaveStdErr();

            Approvals.Verify(commandResult.StdOut);
        }

        [Theory]
        [InlineData("-h")]
        [InlineData("--help")]
        public void CanShowHelp_Update(string option)
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            var commandResult = new DotnetNewCommand(_log, "update", option)
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult.Should().ExitWith(0)
                .And.NotHaveStdErr();

            Approvals.Verify(commandResult.StdOut);
        }

        [Theory]
        [InlineData("-h")]
        [InlineData("--help")]
        public void CanShowHelp_Uninstall(string option)
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            var commandResult = new DotnetNewCommand(_log, "uninstall", option)
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult.Should().ExitWith(0)
                .And.NotHaveStdErr();

            Approvals.Verify(commandResult.StdOut);
        }

        [Theory]
        [InlineData("-h")]
        [InlineData("--help")]
        public void CanShowHelp_List(string option)
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            var commandResult = new DotnetNewCommand(_log, "list", option)
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult.Should().ExitWith(0)
                .And.NotHaveStdErr();

            Approvals.Verify(commandResult.StdOut);
        }

        [Theory]
        [InlineData("-h")]
        [InlineData("--help")]
        public void CanShowHelp_Search(string option)
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            var commandResult = new DotnetNewCommand(_log, "search", option)
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult.Should().ExitWith(0)
                .And.NotHaveStdErr();

            Approvals.Verify(commandResult.StdOut);
        }

        [Theory]
        [InlineData("console -h")]
        [InlineData("console --help")]
        public void CanShowHelpForTemplate_Console(string command)
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            var commandResult = new DotnetNewCommand(_log, command.Split(" "))
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.NotHaveStdOutContaining("Usage: new3 [options]");

            Approvals.Verify(commandResult.StdOut);
        }

        [Theory]
        [InlineData("classlib -h")]
        [InlineData("classlib --help")]
        public void CanShowHelpForTemplate_Classlib(string command)
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            var commandResult = new DotnetNewCommand(_log, command.Split(" "))
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.NotHaveStdOutContaining("Usage: new3 [options]");

            Approvals.Verify(commandResult.StdOut);
        }

        [Theory]
        [InlineData("globaljson -h")]
        public void CanShowHelpForTemplate_GlobalJson(string command)
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            var commandResult = new DotnetNewCommand(_log, command.Split(" "))
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.NotHaveStdOutContaining("Usage: new3 [options]");

            Approvals.Verify(commandResult.StdOut);
        }

        [Fact]
        public void CannotShowHelpForTemplate_PartialNameMatch()
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            var commandResult = new DotnetNewCommand(_log, "class", "-h")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult.Should().Pass().And.NotHaveStdErr();
            Approvals.Verify(commandResult.StdOut);
        }

        [Fact]
        public void CannotShowHelpForTemplate_FullNameMatch()
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            var commandResult = new DotnetNewCommand(_log, "Console App", "-h")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult.Should().Pass().And.NotHaveStdErr();
            Approvals.Verify(commandResult.StdOut);
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "TODO: does not fail now, check if can fail")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void CannotShowHelpForTemplate_WhenAmbiguousLanguageChoice()
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicFSharp", _log, workingDirectory, _fixture.HomeDirectory);
            Helpers.InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicVB", _log, workingDirectory, _fixture.HomeDirectory);

            var commandResult = new DotnetNewCommand(_log, "basic", "--help")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult.Should().Fail().And.NotHaveStdOut();

            Approvals.Verify(commandResult.StdErr);
        }

        [Fact]
        public void CanShowHelpForTemplate_MatchOnChoice()
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            var commandResult = new DotnetNewCommand(_log, "console", "--help", "--framework", "net7.0")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should().Pass()
                .And.NotHaveStdErr()
                .And.NotHaveStdOutContaining("Usage: new3 [options]");

            Approvals.Verify(commandResult.StdOut);
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "Help is not implemented yet")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void CannotShowHelpForTemplate_MatchOnChoiceWithoutValue()
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            var commandResult = new DotnetNewCommand(_log, "console", "--help", "--framework")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should().Fail()
                .And.NotHaveStdOut();

            Approvals.Verify(commandResult.StdErr);
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "Help is not implemented yet")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void CannotShowHelpForTemplate_MatchOnUnexistingParam()
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            var commandResult = new DotnetNewCommand(_log, "console", "--help", "--do-not-exist")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should().Fail()
                .And.NotHaveStdOut();

            Approvals.Verify(commandResult.StdErr);
        }

        [Fact]
        public void CanShowHelpForTemplate_MatchOnNonChoiceParam()
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            var commandResult = new DotnetNewCommand(_log, "console", "--help", "--langVersion", "8.0")
                    .WithCustomHive(_fixture.HomeDirectory)
                    .WithWorkingDirectory(workingDirectory)
                    .Execute();

            commandResult
                    .Should().Pass()
                    .And.NotHaveStdErr()
                    .And.NotHaveStdOutContaining("Usage: new3 [options]");

            Approvals.Verify(commandResult.StdOut);
        }

        [Fact]
        public void CanShowHelpForTemplate_MatchOnLanguage()
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            var commandResult = new DotnetNewCommand(_log, "console", "--help", "--language", "F#")
                    .WithCustomHive(_fixture.HomeDirectory)
                    .WithWorkingDirectory(workingDirectory)
                    .Execute();

            commandResult
                    .Should().Pass()
                    .And.NotHaveStdErr()
                    .And.NotHaveStdOutContaining("Usage: new3 [options]");

            Approvals.Verify(commandResult.StdOut);
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "Help is not implemented yet")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void CannotShowHelpForTemplate_MatchOnNonChoiceParamWithoutValue()
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            var commandResult = new DotnetNewCommand(_log, "console", "--help", "--langVersion")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should().Fail()
                .And.NotHaveStdOut();

            Approvals.Verify(commandResult.StdErr);
        }
    }
}
