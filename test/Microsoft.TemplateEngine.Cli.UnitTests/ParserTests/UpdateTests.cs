// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    public class UpdateTests
    {
        [Theory]
        [InlineData("--add-source")]
        [InlineData("--nuget-source")]
        public void Update_CanParseAddSourceOption(string optionName)
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse($"new update {optionName} my-custom-source");
            UpdateCommandArgs args = new UpdateCommandArgs((UpdateCommand)parseResult.CommandResult.Command, parseResult);

            Assert.Single(args.AdditionalSources);
            Assert.Contains("my-custom-source", args.AdditionalSources);
        }

        [Theory]
        [InlineData("--update-apply")]
        [InlineData("--update-check")]
        [InlineData("update")]
        public void Update_Error_WhenArguments(string commandName)
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse($"new {commandName} source");

            Assert.True(parseResult.Errors.Any());
            Assert.Contains(parseResult.Errors, error => error.Message.Contains("Unrecognized command or argument 'source'"));
        }

        [Theory]
        [InlineData("new update --add-source my-custom-source1 my-custom-source2")]
        [InlineData("new update --check-only --add-source my-custom-source1 --add-source my-custom-source2")]
        public void Update_CanParseAddSourceOption_MultipleEntries(string testCase)
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false), new NewCommandCallbacks());
            var parseResult = myCommand.Parse(testCase);
            UpdateCommandArgs args = new UpdateCommandArgs((UpdateCommand)parseResult.CommandResult.Command, parseResult);

            Assert.Equal(2, args.AdditionalSources.Count);
            Assert.Contains("my-custom-source1", args.AdditionalSources);
            Assert.Contains("my-custom-source2", args.AdditionalSources);
        }

        [Fact]
        public void Update_CanParseInteractiveOption()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse($"new update --interactive");
            UpdateCommandArgs args = new UpdateCommandArgs((UpdateCommand)parseResult.CommandResult.Command, parseResult);

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
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse($"new update {optionAlias}");
            UpdateCommandArgs args = new UpdateCommandArgs((UpdateCommand)parseResult.CommandResult.Command, parseResult);

            Assert.True(args.CheckOnly);

            parseResult = myCommand.Parse($"new update");
            args = new UpdateCommandArgs((UpdateCommand)parseResult.CommandResult.Command, parseResult);

            Assert.False(args.CheckOnly);
        }

        [Fact]
        public void Update_Legacy_CanParseCheckOnlyOption()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse($"new --update-check");
            UpdateCommandArgs args = new UpdateCommandArgs((LegacyUpdateCheckCommand)parseResult.CommandResult.Command, parseResult);

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
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse(testCase);
            UpdateCommandArgs args = new UpdateCommandArgs((BaseUpdateCommand)parseResult.CommandResult.Command, parseResult);

            Assert.Single(args.AdditionalSources);
            Assert.Contains("my-custom-source", args.AdditionalSources);
        }

        [Theory]
        [InlineData("new --update-check source --interactive")]
        [InlineData("new --interactive --update-apply source")]
        public void Update_Legacy_CanParseInteractiveOption(string testCase)
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse(testCase);
            UpdateCommandArgs args = new UpdateCommandArgs((BaseUpdateCommand)parseResult.CommandResult.Command, parseResult);

            Assert.True(args.Interactive);
        }

        [Theory]
        [InlineData("new --update-check --add-source my-custom-source1 --add-source my-custom-source2")]
        [InlineData("new --add-source my-custom-source1 --add-source my-custom-source2 --update-apply source")]
        [InlineData("new --add-source my-custom-source1 --update-apply --add-source my-custom-source2")]
        public void Update_Legacy_CanParseAddSourceOption_MultipleEntries(string testCase)
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false), new NewCommandCallbacks());
            var parseResult = myCommand.Parse(testCase);
            UpdateCommandArgs args = new UpdateCommandArgs((BaseUpdateCommand)parseResult.CommandResult.Command, parseResult);

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
        [InlineData("new source1 source2 source3 --update-apply source", "'source1'|'source'")] //only first custom validation error is added
        [InlineData("new source1 --update-apply source", "'source1'|'source'")]
        public void Update_CanReturnParseError(string command, string expectedInvalidTokens)
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse(command);
            var errorMessages = parseResult.Errors.Select(error => error.Message);

            var expectedInvalidTokenSets = expectedInvalidTokens.Split("|");

            Assert.NotEmpty(parseResult.Errors);
            Assert.Equal(expectedInvalidTokenSets.Length, parseResult.Errors.Count);
            foreach (var tokenSet in expectedInvalidTokenSets)
            {
                Assert.True(errorMessages.Contains($"Unrecognized command or argument(s): {tokenSet}.") || errorMessages.Contains($"Unrecognized command or argument {tokenSet}."));
            }
        }

        [Fact]
        public void CommandExampleCanShowParentCommandsBeyondNew()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false), new NewCommandCallbacks());
            Command rootCommand = new Command("dotnet")
            {
                myCommand
            };

            var parseResult = rootCommand.Parse("dotnet new update");
            Assert.Equal("dotnet new update", Example.For<NewCommand>(parseResult).WithSubcommand<UpdateCommand>());
        }
    }
}
