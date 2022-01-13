// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    public class InstallTests
    {
        [Theory]
        [InlineData("--add-source")]
        [InlineData("--nuget-source")]
        public void Install_CanParseAddSourceOption(string optionName)
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse($"new install source {optionName} my-custom-source");
            InstallCommandArgs args = new InstallCommandArgs((InstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.Single(args.AdditionalSources);
            Assert.Contains("my-custom-source", args.AdditionalSources);
            Assert.Single(args.TemplatePackages);
            Assert.Contains("source", args.TemplatePackages);
        }

        [Fact]
        public void Install_Error_NoArguments()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse($"new install");

            Assert.True(parseResult.Errors.Any());
            Assert.Contains(parseResult.Errors, error => error.Message.Contains("Required argument missing"));

            Assert.Throws<ArgumentException>(() => new InstallCommandArgs((InstallCommand)parseResult.CommandResult.Command, parseResult));
        }

        [Fact]
        public void Install_Legacy_Error_NoArguments()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse($"new --install --interactive");

            Assert.True(parseResult.Errors.Any());
            Assert.Contains(parseResult.Errors, error => error.Message.Contains("Required argument missing"));

            Assert.Throws<ArgumentException>(() => new InstallCommandArgs((LegacyInstallCommand)parseResult.CommandResult.Command, parseResult));
        }

        [Theory]
        [InlineData("new install source --add-source my-custom-source1 my-custom-source2")]
        [InlineData("new install source --add-source my-custom-source1 --add-source my-custom-source2")]
        public void Install_CanParseAddSourceOption_MultipleEntries(string testCase)
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
            var parseResult = myCommand.Parse(testCase);
            InstallCommandArgs args = new InstallCommandArgs((InstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.Equal(2, args.AdditionalSources.Count);
            Assert.Contains("my-custom-source1", args.AdditionalSources);
            Assert.Contains("my-custom-source2", args.AdditionalSources);
            Assert.Single(args.TemplatePackages);
            Assert.Contains("source", args.TemplatePackages);
        }

        [Fact]
        public void Install_CanParseInteractiveOption()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse($"new install source --interactive");
            InstallCommandArgs args = new InstallCommandArgs((InstallCommand)parseResult.CommandResult.Command, parseResult);

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
        public void Install_CanParseMultipleArgs()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse($"new install source1 source2");
            InstallCommandArgs args = new InstallCommandArgs((InstallCommand)parseResult.CommandResult.Command, parseResult);

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
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse(testCase);
            InstallCommandArgs args = new InstallCommandArgs((LegacyInstallCommand)parseResult.CommandResult.Command, parseResult);

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
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse(testCase);
            InstallCommandArgs args = new InstallCommandArgs((LegacyInstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.True(args.Interactive);
            Assert.Single(args.TemplatePackages);
            Assert.Contains("source", args.TemplatePackages);
        }

        [Theory]
        [InlineData("new --install source1 --install source2")]
        [InlineData("new --install source1 source2")]
        public void Install_Legacy_CanParseMultipleArgs(string testCase)
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
             
            var parseResult = myCommand.Parse(testCase);
            InstallCommandArgs args = new InstallCommandArgs((LegacyInstallCommand)parseResult.CommandResult.Command, parseResult);

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
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
            var parseResult = myCommand.Parse(testCase);
            InstallCommandArgs args = new InstallCommandArgs((LegacyInstallCommand)parseResult.CommandResult.Command, parseResult);

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
        [InlineData("new source1 source2 source3 --install source", "'source1'")] //only first error is added
        [InlineData("new source1 --install source", "'source1'")]
        public void Install_CanReturnParseError(string command, string expectedInvalidTokens)
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
                Assert.True(errorMessages.Contains($"Unrecognized command or argument(s): {tokenSet}.") || errorMessages.Contains($"Unrecognized command or argument {tokenSet}."));
            }
        }

    }
}
