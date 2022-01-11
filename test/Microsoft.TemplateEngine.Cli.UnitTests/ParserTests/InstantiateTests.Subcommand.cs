// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Parsing;
using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Mocks;
//using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    public partial class InstantiateTests
    {
        [Fact]
        public void Create_CanParseTemplateWithOptions()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
            var parseResult = myCommand.Parse("new create console --framework net5.0");
            InstantiateCommandArgs args = new InstantiateCommandArgs((InstantiateCommand)parseResult.CommandResult.Command, parseResult);

            Assert.Equal("console", args.ShortName);
            Assert.Equal(2, args.RemainingArguments.Count());
            Assert.Contains("--framework", args.RemainingArguments);
            Assert.Contains("net5.0", args.RemainingArguments);
        }

        [Theory]
        [MemberData(nameof(CanEvaluateTemplateToRunData))]
        internal void Create_CanEvaluateTemplateToRun(string command, string templateSet, string? defaultLanguage, string? expectedIdentitiesStr)
        {
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(_testSets[templateSet], A.Fake<IHostSpecificDataLoader>()))
                .Single();

            string[] expectedIdentities = expectedIdentitiesStr?.Split("|") ?? Array.Empty<string>();

            var defaultParams = new Dictionary<string, string>();
            if (defaultLanguage != null)
            {
                defaultParams["prefs:language"] = defaultLanguage;
            }

            ITemplateEngineHost host = TestHost.GetVirtualHost(defaultParameters: defaultParams);
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
            var parseResult = myCommand.Parse($"new create {command}");
            var instantiateCommand = (InstantiateCommand)parseResult.CommandResult.Command;
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);
            var templateCommands = instantiateCommand.GetTemplateCommand(args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Equal(expectedIdentities.Count(), templateCommands.Count);
            Assert.Equal(expectedIdentities.OrderBy(s => s), templateCommands.Select(templateCommand => templateCommand.Template.Identity).OrderBy(s => s));
        }

        [Theory]
        [MemberData(nameof(CanParseNameOptionData))]
        internal void Create_CanParseNameOption(string command, string? expectedValue)
        {
            var template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group");

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
                .Single();

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
            var parseResult = myCommand.Parse($"new create {command}");
            var instantiateCommand = (InstantiateCommand)parseResult.CommandResult.Command;
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);
            TemplateCommand templateCommand = new TemplateCommand(instantiateCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());
            Parser parser = ParserFactory.CreateParser(templateCommand);
            ParseResult templateParseResult = parser.Parse(args.RemainingArguments ?? Array.Empty<string>());
            var templateArgs = new TemplateCommandArgs(templateCommand, templateParseResult);

            Assert.Equal(expectedValue, templateArgs.Name);
        }

        [Theory]
        [MemberData(nameof(CanParseTemplateOptionsData))]
        internal void Create_CanParseTemplateOptions(string command, string parameterName, string parameterType, string? defaultValue, string? defaultIfNoOptionValue, string? expectedValue)
        {
            //unique case for dotnet new create
            if (command == "foo -in 30")
            {
                command = "foo -i 30"; //for dotnet new create "-i" is not occupied, so we can use it.
            }

            var template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithParameter(parameterName, parameterType, defaultValue: defaultValue, defaultIfNoOptionValue: defaultIfNoOptionValue);

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
                .Single();

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());  
            var parseResult = myCommand.Parse($"new create {command}");
            var instantiateCommand = (InstantiateCommand)parseResult.CommandResult.Command;
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);
            TemplateCommand templateCommand = new TemplateCommand(instantiateCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());
            Parser parser = ParserFactory.CreateParser(templateCommand);
            ParseResult templateParseResult = parser.Parse(args.RemainingArguments ?? Array.Empty<string>());
            var templateArgs = new TemplateCommandArgs(templateCommand, templateParseResult);

            if (string.IsNullOrWhiteSpace(expectedValue))
            {
                Assert.False(templateArgs.TemplateParameters.ContainsKey(parameterName));
            }
            else
            {
                Assert.True(templateArgs.TemplateParameters.ContainsKey(parameterName));
                Assert.Equal(expectedValue, templateArgs.TemplateParameters[parameterName]);
            }
        }

        [Theory]
        [MemberData(nameof(CanParseChoiceTemplateOptionsData))]
        internal void Create_CanParseChoiceTemplateOptions(string command, string parameterName, string parameterValues, string? defaultIfNoOptionValue, string? expectedValue)
        {
            var template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter(parameterName, parameterValues.Split("|"), defaultIfNoOptionValue: defaultIfNoOptionValue);

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
                .Single();

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
            var parseResult = myCommand.Parse($"new create {command}");
            var instantiateCommand = (InstantiateCommand)parseResult.CommandResult.Command;
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);
            TemplateCommand templateCommand = new TemplateCommand(instantiateCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());
            Parser parser = ParserFactory.CreateParser(templateCommand);
            ParseResult templateParseResult = parser.Parse(args.RemainingArguments ?? Array.Empty<string>());
            var templateArgs = new TemplateCommandArgs(templateCommand, templateParseResult);

            if (string.IsNullOrWhiteSpace(expectedValue))
            {
                Assert.False(templateArgs.TemplateParameters.ContainsKey(parameterName));
            }
            else
            {
                Assert.True(templateArgs.TemplateParameters.ContainsKey(parameterName));
                Assert.Equal(expectedValue, templateArgs.TemplateParameters[parameterName]);
            }
        }

        [Theory]
        [MemberData(nameof(CanDetectParseErrorsTemplateOptionsData))]
        internal void Create_CanDetectParseErrorsTemplateOptions(
            string command,
            string parameterName,
            string parameterType,
            bool isRequired,
            string? defaultValue,
            string? defaultIfNoOptionValue,
            string expectedError)
        {
            var template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithParameter(parameterName, parameterType, isRequired, defaultValue: defaultValue, defaultIfNoOptionValue: defaultIfNoOptionValue);

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
                .Single();

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
            var parseResult = myCommand.Parse($"new create {command}");
            var instantiateCommand = (InstantiateCommand)parseResult.CommandResult.Command;
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);

            TemplateCommand templateCommand = new TemplateCommand(instantiateCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());
            Parser parser = ParserFactory.CreateParser(templateCommand);
            ParseResult templateParseResult = parser.Parse(args.RemainingArguments ?? Array.Empty<string>());
            Assert.True(templateParseResult.Errors.Any());
            Assert.Equal(expectedError, templateParseResult.Errors.Single().Message);
        }

        [Theory]
        [MemberData(nameof(CanDetectParseErrorsChoiceTemplateOptionsData))]
        internal void Create_CanDetectParseErrorsChoiceTemplateOptions(
              string command,
              string parameterName,
              string parameterValues,
              bool isRequired,
              string? defaultValue,
              string? defaultIfNoOptionValue,
              string expectedError)
        {
            var template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter(parameterName, parameterValues.Split("|"), isRequired, defaultValue, defaultIfNoOptionValue);

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
                .Single();

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
            var parseResult = myCommand.Parse($" new create {command}");
            var instantiateCommand = (InstantiateCommand)parseResult.CommandResult.Command;
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);

            TemplateCommand templateCommand = new TemplateCommand(instantiateCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());
            Parser parser = ParserFactory.CreateParser(templateCommand);
            ParseResult templateParseResult = parser.Parse(args.RemainingArguments ?? Array.Empty<string>());
            Assert.True(templateParseResult.Errors.Any());
            Assert.Equal(expectedError, templateParseResult.Errors.Single().Message);
        }

        [Theory]
        [InlineData("create", "createTemplate")]
        [InlineData("list", "listTemplate")]
        internal void Create_CanEvaluateTemplateWithSubcommandShortName(string command, string? expectedIdentitiesStr)
        {
            MockTemplateInfo template = new MockTemplateInfo(command, identity: $"{command}Template");

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
                .Single();

            string[] expectedIdentities = expectedIdentitiesStr?.Split("|") ?? Array.Empty<string>();
            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
            var parseResult = myCommand.Parse($"new create {command}");
            var instantiateCommand = (InstantiateCommand)parseResult.CommandResult.Command;
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);
            var templateCommands = instantiateCommand.GetTemplateCommand(args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Equal(expectedIdentities.Count(), templateCommands.Count);
            Assert.Equal(expectedIdentities.OrderBy(s => s), templateCommands.Select(templateCommand => templateCommand.Template.Identity).OrderBy(s => s));
        }
    }
}
