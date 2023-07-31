// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    [UsesVerify]
    public partial class DotnetNewHelpTests : BaseIntegrationTest
    {
        [Theory]
        [InlineData("-h")]
        [InlineData("/h")]
        [InlineData("--help")]
        [InlineData("-?")]
        [InlineData("/?")]
        public Task CanShowHelp(string command)
        {
            string workingDirectory = CreateTemporaryFolder();

            CommandResult commandResult = new DotnetNewCommand(_log, command)
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult.Should().ExitWith(0)
                .And.NotHaveStdErr();

            return Verify(commandResult.StdOut)
                .UseTextForParameters("common")
                .DisableRequireUniquePrefix();
        }

        [Theory]
        [InlineData("-h")]
        [InlineData("--help")]
        public Task CanShowHelp_Create(string option)
        {
            string workingDirectory = CreateTemporaryFolder();

            CommandResult commandResult = new DotnetNewCommand(_log, "create", option)
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult.Should().ExitWith(0)
                .And.NotHaveStdErr();

            return Verify(commandResult.StdOut)
                .UseTextForParameters("common")
                .DisableRequireUniquePrefix();
        }

        [Theory]
        [InlineData("-h")]
        [InlineData("--help")]
        public Task CanShowHelp_Install(string option)
        {
            string workingDirectory = CreateTemporaryFolder();

            CommandResult commandResult = new DotnetNewCommand(_log, "install", option)
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult.Should().ExitWith(0)
                .And.NotHaveStdErr();

            return Verify(commandResult.StdOut)
                .UseTextForParameters("common")
                .DisableRequireUniquePrefix();
        }

        [Theory]
        [InlineData("-h")]
        [InlineData("--help")]
        public Task CanShowHelp_Update(string option)
        {
            string workingDirectory = CreateTemporaryFolder();

            CommandResult commandResult = new DotnetNewCommand(_log, "update", option)
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult.Should().ExitWith(0)
                .And.NotHaveStdErr();

            return Verify(commandResult.StdOut)
                .UseTextForParameters("common")
                .DisableRequireUniquePrefix();
        }

        [Theory]
        [InlineData("-h")]
        [InlineData("--help")]
        public Task CanShowHelp_Uninstall(string option)
        {
            string workingDirectory = CreateTemporaryFolder();

            CommandResult commandResult = new DotnetNewCommand(_log, "uninstall", option)
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult.Should().ExitWith(0)
                .And.NotHaveStdErr();

            return Verify(commandResult.StdOut)
                 .UseTextForParameters("common")
                .DisableRequireUniquePrefix();
        }

        [Theory]
        [InlineData("-h")]
        [InlineData("--help")]
        public Task CanShowHelp_List(string option)
        {
            string workingDirectory = CreateTemporaryFolder();

            CommandResult commandResult = new DotnetNewCommand(_log, "list", option)
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult.Should().ExitWith(0)
                .And.NotHaveStdErr();

            return Verify(commandResult.StdOut)
                .UseTextForParameters("common")
                .DisableRequireUniquePrefix();
        }

        [Theory]
        [InlineData("-h")]
        [InlineData("--help")]
        public Task CanShowHelp_Search(string option)
        {
            string workingDirectory = CreateTemporaryFolder();

            CommandResult commandResult = new DotnetNewCommand(_log, "search", option)
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult.Should().ExitWith(0)
                .And.NotHaveStdErr();

            return Verify(commandResult.StdOut)
                .UseTextForParameters("common")
                .DisableRequireUniquePrefix();
        }

        [Theory]
        [InlineData("console -h", "console")]
        [InlineData("console --help", "console")]
        [InlineData("classlib -h", "classlib")]
        [InlineData("classlib --help", "classlib")]
        [InlineData("globaljson -h", "globaljson")]
        public Task CanShowHelpForTemplate(string command, string setName)
        {
            string workingDirectory = CreateTemporaryFolder();

            CommandResult commandResult = new DotnetNewCommand(_log, command.Split(" "))
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.NotHaveStdOutContaining("Usage: new [options]");

            return Verify(commandResult.StdOut)
                .UseTextForParameters(setName)
                .DisableRequireUniquePrefix();
        }

        [Fact]
        public Task CannotShowHelpForTemplate_PartialNameMatch()
        {
            string workingDirectory = CreateTemporaryFolder();

            CommandResult commandResult = new DotnetNewCommand(_log, "classli", "-h")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult.Should().Pass().And.NotHaveStdErr();
            return Verify(commandResult.StdOut);
        }

        [Fact]
        public Task CannotShowHelpForTemplate_FullNameMatch()
        {
            string workingDirectory = CreateTemporaryFolder();

            CommandResult commandResult = new DotnetNewCommand(_log, "Console App", "-h")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            //help command cannot fail, therefore the output is written to stdout
            commandResult.Should().Pass().And.NotHaveStdErr();
            return Verify(commandResult.StdOut);
        }

        [Fact]
        public Task CannotShowHelpForTemplate_WhenAmbiguousLanguageChoice()
        {
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicFSharp", _log, _fixture.HomeDirectory, workingDirectory);
            InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicVB", _log, _fixture.HomeDirectory, workingDirectory);

            CommandResult commandResult = new DotnetNewCommand(_log, "basic", "--help")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            //help command cannot fail, therefore the output is written to stdout
            commandResult.Should().Pass().And.NotHaveStdErr();
            return Verify(commandResult.StdOut);
        }

        [Fact]
        public Task CanShowHelpForTemplate_MultipleValueChoice()
        {
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate("TemplateWithMultiValueChoice", _log, _fixture.HomeDirectory, workingDirectory);

            CommandResult commandResult = new DotnetNewCommand(_log, "TestAssets.TemplateWithMultiValueChoice", "--help")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            //help command should not fail, therefore the output is written to stdout
            commandResult.Should().Pass().And.NotHaveStdErr();
            return Verify(commandResult.StdOut);
        }

        [Fact]
        public Task CanShowHelpForTemplate_MatchOnChoice()
        {
            string workingDirectory = CreateTemporaryFolder();

            CommandResult commandResult = new DotnetNewCommand(_log, "console", "--help", "--framework", "net7.0")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should().Pass()
                .And.NotHaveStdErr()
                .And.NotHaveStdOutContaining("Usage: new [options]");

            return Verify(commandResult.StdOut);
        }

        [Fact]
        public Task CannotShowHelpForTemplate_MatchOnChoiceWithoutValue()
        {
            string workingDirectory = CreateTemporaryFolder();

            CommandResult commandResult = new DotnetNewCommand(_log, "console", "--help", "--framework")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            //help command cannot fail, therefore the output is written to stdout
            commandResult.Should().Pass().And.NotHaveStdErr();
            return Verify(commandResult.StdOut);
        }

        [Fact]
        public Task CannotShowHelpForTemplate_MatchOnUnexistingParam()
        {
            string workingDirectory = CreateTemporaryFolder();

            CommandResult commandResult = new DotnetNewCommand(_log, "console", "--help", "--do-not-exist")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            //help command cannot fail, therefore the output is written to stdout
            commandResult.Should().Pass().And.NotHaveStdErr();
            return Verify(commandResult.StdOut);
        }

        [Fact]
        public Task CanShowHelpForTemplate_MatchOnNonChoiceParam()
        {
            string workingDirectory = CreateTemporaryFolder();

            CommandResult commandResult = new DotnetNewCommand(_log, "console", "--help", "--langVersion", "8.0")
                    .WithCustomHive(_fixture.HomeDirectory)
                    .WithWorkingDirectory(workingDirectory)
                    .Execute();

            //help command cannot fail, therefore the output is written to stdout
            commandResult.Should().Pass().And.NotHaveStdErr().And.NotHaveStdOutContaining("Usage: new [options]");
            return Verify(commandResult.StdOut);
        }

        [Fact]
        public Task CanShowHelpForTemplate_MatchOnLanguage()
        {
            string workingDirectory = CreateTemporaryFolder();

            CommandResult commandResult = new DotnetNewCommand(_log, "console", "--help", "--language", "F#")
                    .WithCustomHive(_fixture.HomeDirectory)
                    .WithWorkingDirectory(workingDirectory)
                    .Execute();

            commandResult
                    .Should().Pass()
                    .And.NotHaveStdErr()
                    .And.NotHaveStdOutContaining("Usage: new [options]");

            return Verify(commandResult.StdOut);
        }

        [Fact]
        public Task CannotShowHelpForTemplate_MatchOnNonChoiceParamWithoutValue()
        {
            string workingDirectory = CreateTemporaryFolder();

            CommandResult commandResult = new DotnetNewCommand(_log, "console", "--help", "--langVersion")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            //help command cannot fail, therefore the output is written to stdout
            commandResult.Should().Pass().And.NotHaveStdErr();
            return Verify(commandResult.StdOut);
        }

        [Fact]
        public Task CanShowAllowScriptsOption()
        {
            string templateLocation = "PostActions/RunScript/Basic";
            string templateName = "TestAssets.PostActions.RunScript.Basic";
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            CommandResult commandResult = new DotnetNewCommand(_log, templateName, "--help")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult.Should().Pass().And.NotHaveStdErr();
            return Verify(commandResult.StdOut);
        }

        [Fact]
        public Task CanShowHelpForTemplate_RequiredParams()
        {
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate("TemplateWithRequiredParameters", _log, _fixture.HomeDirectory, workingDirectory);

            CommandResult commandResult = new DotnetNewCommand(_log, "TestAssets.TemplateWithRequiredParameters", "--help")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            return Verify(commandResult.StdOut);
        }

        [Fact]
        public Task CanShowHelpForTemplate_ConditionalParams()
        {
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate("TemplateWithConditionalParameters", _log, _fixture.HomeDirectory, workingDirectory);

            CommandResult commandResult = new DotnetNewCommand(_log, "TestAssets.TemplateWithConditionalParameters", "--help")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            return Verify(commandResult.StdOut);
        }

        [Fact]
        public Task CanShowHelpForTemplateWhenRequiredParamIsMissed()
        {
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate($"TemplateResolution/MissedRequiredParameter/BasicTemplate1", _log, _fixture.HomeDirectory, workingDirectory);

            CommandResult commandResult = new DotnetNewCommand(_log, "basic", "--help")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            return Verify(commandResult.StdOut);
        }

        [Fact]
        public Task CanShowHelpForTemplateWhenRequiredParamIsMissedAndConditionIntroduced()
        {
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate($"TemplateResolution/MissedRequiredParameter/BasicTemplate2", _log, _fixture.HomeDirectory, workingDirectory);

            CommandResult commandResult = new DotnetNewCommand(_log, "basic2", "--help")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            return Verify(commandResult.StdOut);
        }
    }
}
