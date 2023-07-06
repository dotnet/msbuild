// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    public class UpdateTests : BaseTest
    {
        [Theory]
        [InlineData("--add-source")]
        [InlineData("--nuget-source")]
        public void Update_CanParseAddSourceOption(string optionName)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse($"new update {optionName} my-custom-source");
            UpdateCommandArgs args = new((UpdateCommand)parseResult.CommandResult.Command, parseResult);

            Assert.NotNull(args.AdditionalSources);
            Assert.Single(args.AdditionalSources);
            Assert.Contains("my-custom-source", args.AdditionalSources);
        }

        [Theory]
        [InlineData("--update-apply")]
        [InlineData("--update-check")]
        [InlineData("update")]
        public void Update_Error_WhenArguments(string commandName)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse($"new {commandName} source");

            Assert.True(parseResult.Errors.Any());
            Assert.Contains(parseResult.Errors, error => error.Message.Contains("Unrecognized command or argument 'source'"));
        }

        [Theory]
        [InlineData("new update --add-source my-custom-source1 my-custom-source2")]
        [InlineData("new update --check-only --add-source my-custom-source1 --add-source my-custom-source2")]
        public void Update_CanParseAddSourceOption_MultipleEntries(string testCase)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse(testCase);
            UpdateCommandArgs args = new((UpdateCommand)parseResult.CommandResult.Command, parseResult);

            Assert.NotNull(args.AdditionalSources);
            Assert.Equal(2, args.AdditionalSources.Count);
            Assert.Contains("my-custom-source1", args.AdditionalSources);
            Assert.Contains("my-custom-source2", args.AdditionalSources);
        }

        [Fact]
        public void Update_CanParseInteractiveOption()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse($"new update --interactive");
            UpdateCommandArgs args = new((UpdateCommand)parseResult.CommandResult.Command, parseResult);

            Assert.True(args.Interactive);

            parseResult = myCommand.Parse($"new update");
            args = new UpdateCommandArgs((UpdateCommand)parseResult.CommandResult.Command, parseResult);

            Assert.False(args.Interactive);
        }

        [Theory]
        [InlineData("--check-only")]
        [InlineData("--dry-run")]
        public void Update_CanParseCheckOnlyOption(string optionAlias)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse($"new update {optionAlias}");
            UpdateCommandArgs args = new((UpdateCommand)parseResult.CommandResult.Command, parseResult);

            Assert.True(args.CheckOnly);

            parseResult = myCommand.Parse($"new update");
            args = new UpdateCommandArgs((UpdateCommand)parseResult.CommandResult.Command, parseResult);

            Assert.False(args.CheckOnly);
        }

        [Fact]
        public void Update_Legacy_CanParseCheckOnlyOption()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse($"new --update-check");
            UpdateCommandArgs args = new((LegacyUpdateCheckCommand)parseResult.CommandResult.Command, parseResult);

            Assert.True(args.CheckOnly);

            parseResult = myCommand.Parse($"new --update-apply");
            args = new UpdateCommandArgs((LegacyUpdateApplyCommand)parseResult.CommandResult.Command, parseResult);

            Assert.False(args.CheckOnly);
        }

        [Theory]
        [InlineData("new --update-check --add-source my-custom-source")]
        [InlineData("new --update-apply --nuget-source my-custom-source")]
        [InlineData("new --nuget-source my-custom-source --update-apply")]
        public void Update_Legacy_CanParseAddSourceOption(string testCase)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse(testCase);
            UpdateCommandArgs args = new((BaseUpdateCommand)parseResult.CommandResult.Command, parseResult);

            Assert.NotNull(args.AdditionalSources);
            Assert.Single(args.AdditionalSources);
            Assert.Contains("my-custom-source", args.AdditionalSources);
        }

        [Theory]
        [InlineData("new --update-check source --interactive")]
        [InlineData("new --interactive --update-apply source")]
        public void Update_Legacy_CanParseInteractiveOption(string testCase)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse(testCase);
            UpdateCommandArgs args = new((BaseUpdateCommand)parseResult.CommandResult.Command, parseResult);

            Assert.True(args.Interactive);
        }

        [Theory]
        [InlineData("new --update-check --add-source my-custom-source1 --add-source my-custom-source2")]
        [InlineData("new --add-source my-custom-source1 --add-source my-custom-source2 --update-apply source")]
        [InlineData("new --add-source my-custom-source1 --update-apply --add-source my-custom-source2")]
        public void Update_Legacy_CanParseAddSourceOption_MultipleEntries(string testCase)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse(testCase);
            UpdateCommandArgs args = new((BaseUpdateCommand)parseResult.CommandResult.Command, parseResult);

            Assert.NotNull(args.AdditionalSources);
            Assert.Equal(2, args.AdditionalSources.Count);
            Assert.Contains("my-custom-source1", args.AdditionalSources);
            Assert.Contains("my-custom-source2", args.AdditionalSources);
        }

        [Theory]
        [InlineData("new --add-source my-custom-source update source", "'--add-source','my-custom-source'|'source'")]
        [InlineData("new --interactive update source", "'--interactive'|'source'")]
        [InlineData("new --language F# --update-check", "'--language','F#'")]
        [InlineData("new --language F# --update-apply", "'--language','F#'")]
        [InlineData("new --language F# update", "'--language','F#'")]
        [InlineData("new source1 source2 source3 --update-apply source", "'source1'|'source'|'source2','source3'")]
        [InlineData("new source1 --update-apply source", "'source1'|'source'")]
        public void Update_CanReturnParseError(string command, string expectedInvalidTokens)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse(command);
            IEnumerable<string> errorMessages = parseResult.Errors.Select(error => error.Message);

            string[] expectedInvalidTokenSets = expectedInvalidTokens.Split("|");

            Assert.NotEmpty(parseResult.Errors);
            Assert.Equal(expectedInvalidTokenSets.Length, parseResult.Errors.Count);
            foreach (string tokenSet in expectedInvalidTokenSets)
            {
                Assert.True(errorMessages.Contains($"Unrecognized command or argument(s): {tokenSet}.") || errorMessages.Contains($"Unrecognized command or argument {tokenSet}."));
            }
        }

        [Fact]
        public void CommandExampleCanShowParentCommandsBeyondNew()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            CliCommand rootCommand = new("dotnet")
            {
                myCommand
            };

            ParseResult parseResult = rootCommand.Parse("dotnet new update");
            Assert.Equal("dotnet new update", Example.For<NewCommand>(parseResult).WithSubcommand<UpdateCommand>());
        }
    }
}
