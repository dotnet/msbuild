// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    public class InstallTests : BaseTest
    {
        [Theory]
        [InlineData("--add-source")]
        [InlineData("--nuget-source")]
        public void Install_CanParseAddSourceOption(string optionName)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse($"new install source {optionName} my-custom-source");
            InstallCommandArgs args = new((InstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.NotNull(args.AdditionalSources);
            Assert.Single(args.AdditionalSources);
            Assert.Contains("my-custom-source", args.AdditionalSources);
            Assert.Single(args.TemplatePackages);
            Assert.Contains("source", args.TemplatePackages);
        }

        [Fact]
        public void Install_Error_NoArguments()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse($"new install");

            Assert.True(parseResult.Errors.Any());
            Assert.Contains(parseResult.Errors, error => error.Message.Contains("Required argument missing"));

            Assert.Throws<ArgumentException>(() => new InstallCommandArgs((InstallCommand)parseResult.CommandResult.Command, parseResult));
        }

        [Fact]
        public void Install_Legacy_Error_NoArguments()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse($"new --install --interactive");

            Assert.True(parseResult.Errors.Any());
            Assert.Contains(parseResult.Errors, error => error.Message.Contains("Required argument missing"));

            Assert.Throws<ArgumentException>(() => new InstallCommandArgs((LegacyInstallCommand)parseResult.CommandResult.Command, parseResult));
        }

        [Theory]
        [InlineData("new install source --add-source my-custom-source1 my-custom-source2")]
        [InlineData("new install source --add-source my-custom-source1 --add-source my-custom-source2")]
        public void Install_CanParseAddSourceOption_MultipleEntries(string testCase)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse(testCase);
            InstallCommandArgs args = new((InstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.NotNull(args.AdditionalSources);
            Assert.Equal(2, args.AdditionalSources.Count);
            Assert.Contains("my-custom-source1", args.AdditionalSources);
            Assert.Contains("my-custom-source2", args.AdditionalSources);
            Assert.Single(args.TemplatePackages);
            Assert.Contains("source", args.TemplatePackages);
        }

        [Fact]
        public void Install_CanParseInteractiveOption()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse($"new install source --interactive");
            InstallCommandArgs args = new((InstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.True(args.Interactive);
            Assert.Single(args.TemplatePackages);
            Assert.Contains("source", args.TemplatePackages);

            parseResult = myCommand.Parse($"new install source");
            args = new InstallCommandArgs((InstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.False(args.Interactive);
            Assert.Single(args.TemplatePackages);
            Assert.Contains("source", args.TemplatePackages);
        }

        [Fact]
        public void Install_CanParseForceOption()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse($"new install source --force");
            InstallCommandArgs args = new((InstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.True(args.Force);
            Assert.Single(args.TemplatePackages);
            Assert.Contains("source", args.TemplatePackages);

            parseResult = myCommand.Parse($"new install source");
            args = new InstallCommandArgs((InstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.False(args.Force);
            Assert.Single(args.TemplatePackages);
            Assert.Contains("source", args.TemplatePackages);
        }

        [Fact]
        public void Install_CanParseMultipleArgs()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse($"new install source1 source2");
            InstallCommandArgs args = new((InstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.Equal(2, args.TemplatePackages.Count);
            Assert.Contains("source1", args.TemplatePackages);
            Assert.Contains("source2", args.TemplatePackages);
        }

        [Theory]
        [InlineData("new --install source --add-source my-custom-source")]
        [InlineData("new --install source --nuget-source my-custom-source")]
        [InlineData("new --nuget-source my-custom-source --install source")]
        public void Install_Legacy_CanParseAddSourceOption(string testCase)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse(testCase);
            InstallCommandArgs args = new((LegacyInstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.NotNull(args.AdditionalSources);
            Assert.Single(args.AdditionalSources);
            Assert.Contains("my-custom-source", args.AdditionalSources);
            Assert.Single(args.TemplatePackages);
            Assert.Contains("source", args.TemplatePackages);
        }

        [Theory]
        [InlineData("new --install source --interactive")]
        [InlineData("new --interactive --install source")]
        public void Install_Legacy_CanParseInteractiveOption(string testCase)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse(testCase);
            InstallCommandArgs args = new((LegacyInstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.True(args.Interactive);
            Assert.Single(args.TemplatePackages);
            Assert.Contains("source", args.TemplatePackages);
        }

        [Theory]
        [InlineData("new --install source1 --install source2")]
        [InlineData("new --install source1 source2")]
        public void Install_Legacy_CanParseMultipleArgs(string testCase)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse(testCase);
            InstallCommandArgs args = new((LegacyInstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.Equal(2, args.TemplatePackages.Count);
            Assert.Contains("source1", args.TemplatePackages);
            Assert.Contains("source2", args.TemplatePackages);
        }

        [Theory]
        [InlineData("new --install source --add-source my-custom-source1 --add-source my-custom-source2")]
        [InlineData("new --add-source my-custom-source1 --add-source my-custom-source2 --install source")]
        [InlineData("new --add-source my-custom-source1 --install source --add-source my-custom-source2")]
        public void Install_Legacy_CanParseAddSourceOption_MultipleEntries(string testCase)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse(testCase);
            InstallCommandArgs args = new((LegacyInstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.NotNull(args.AdditionalSources);
            Assert.Equal(2, args.AdditionalSources.Count);
            Assert.Contains("my-custom-source1", args.AdditionalSources);
            Assert.Contains("my-custom-source2", args.AdditionalSources);
            Assert.Single(args.TemplatePackages);
            Assert.Contains("source", args.TemplatePackages);
        }

        [Theory]
        [InlineData("new --add-source my-custom-source install source", "'--add-source','my-custom-source'")]
        [InlineData("new --interactive install source", "'--interactive'")]
        [InlineData("new --language F# --install source", "'--language','F#'")]
        [InlineData("new --language F# install source", "'--language','F#'")]
        [InlineData("new source1 source2 source3 --install source", "'source1'|'source2','source3'")]
        [InlineData("new source1 --install source", "'source1'")]
        public void Install_CanReturnParseError(string command, string expectedInvalidTokens)
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

            ParseResult parseResult = rootCommand.Parse("dotnet new install source");
            Assert.Equal("dotnet new install my-source", Example.For<NewCommand>(parseResult).WithSubcommand<InstallCommand>().WithArgument(BaseInstallCommand.NameArgument, "my-source"));
        }

    }
}
