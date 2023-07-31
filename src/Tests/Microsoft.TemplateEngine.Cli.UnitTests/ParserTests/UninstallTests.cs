// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    public class UninstallTests : BaseTest
    {
        [Theory]
        [InlineData("--uninstall")]
        [InlineData("-u")]
        [InlineData("uninstall")]
        public void Uninstall_NoArguments(string commandName)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse($"new {commandName}");
            UninstallCommandArgs args = new((BaseUninstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.Empty(parseResult.Errors);
            Assert.Empty(args.TemplatePackages);
        }

        [Theory]
        [InlineData("--uninstall")]
        [InlineData("-u")]
        [InlineData("uninstall")]
        public void Uninstall_WithArgument(string commandName)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse($"new {commandName} source");
            UninstallCommandArgs args = new((BaseUninstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.Empty(parseResult.Errors);
            Assert.Single(args.TemplatePackages);
            Assert.Contains("source", args.TemplatePackages);
        }

        [Theory]
        [InlineData("new --uninstall source1 --uninstall source2")]
        [InlineData("new --uninstall source1 -u source2")]
        [InlineData("new uninstall source1 source2")]
        public void Uninstall_WithMultipleArgument(string command)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse(command);
            UninstallCommandArgs args = new((BaseUninstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.Empty(parseResult.Errors);
            Assert.Equal(2, args.TemplatePackages.Count);
            Assert.Contains("source1", args.TemplatePackages);
            Assert.Contains("source2", args.TemplatePackages);
        }

        [Theory]
        [InlineData("new --add-source my-custom-source uninstall source", "'--add-source','my-custom-source'")]
        [InlineData("new --interactive uninstall source", "'--interactive'")]
        [InlineData("new --language F# --uninstall source", "'--language','F#'")]
        [InlineData("new --language F# uninstall source", "'--language','F#'")]
        [InlineData("new source1 source2 source3 --uninstall source", "'source1'|'source2','source3'")]
        [InlineData("new source1 --uninstall source", "'source1'")]
        public void Uninstall_CanReturnParseError(string command, string expectedInvalidTokens)
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

            ParseResult parseResult = rootCommand.Parse("dotnet new uninstall source");
            Assert.Equal("dotnet new uninstall my-source", Example.For<NewCommand>(parseResult).WithSubcommand<UninstallCommand>().WithArgument(UninstallCommand.NameArgument, "my-source"));
        }
    }
}
