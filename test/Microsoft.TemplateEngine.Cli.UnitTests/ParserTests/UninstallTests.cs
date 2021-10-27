// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    public class UninstallTests
    {
        [Theory]
        [InlineData("--uninstall")]
        [InlineData("-u")]
        [InlineData("uninstall")]
        public void Uninstall_NoArguments(string commandName)
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse($"new {commandName}");
            UninstallCommandArgs args = new UninstallCommandArgs((BaseUninstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.Empty(parseResult.Errors);
            Assert.Empty(args.TemplatePackages);
        }

        [Theory]
        [InlineData("--uninstall")]
        [InlineData("-u")]
        [InlineData("uninstall")]
        public void Uninstall_WithArgument(string commandName)
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse($"new {commandName} source");
            UninstallCommandArgs args = new UninstallCommandArgs((BaseUninstallCommand)parseResult.CommandResult.Command, parseResult);

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
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse(command);
            UninstallCommandArgs args = new UninstallCommandArgs((BaseUninstallCommand)parseResult.CommandResult.Command, parseResult);

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
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse(command);
            var errorMessages = parseResult.Errors.Select(error => error.Message);

            var expectedInvalidTokenSets = expectedInvalidTokens.Split("|");

            Assert.NotEmpty(parseResult.Errors);
            Assert.Equal(expectedInvalidTokenSets.Length, parseResult.Errors.Count);
            foreach (var tokenSet in expectedInvalidTokenSets)
            {
                Assert.True(errorMessages.Contains($"Unrecognized command or argument(s): {tokenSet}") || errorMessages.Contains($"Unrecognized command or argument {tokenSet}"));
            }
        }
    }
}
