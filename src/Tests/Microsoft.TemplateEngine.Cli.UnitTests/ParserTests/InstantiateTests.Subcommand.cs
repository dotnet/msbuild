// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using FakeItEasy;
using FluentAssertions;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    public partial class InstantiateTests
    {
        [Fact]
        public void Create_CanParseTemplateWithOptions()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse("new create console --framework net5.0");
            InstantiateCommandArgs args = new((InstantiateCommand)parseResult.CommandResult.Command, parseResult);

            Assert.Equal("console", args.ShortName);
            Assert.Equal(2, args.RemainingArguments.Length);
            Assert.Contains("--framework", args.RemainingArguments);
            Assert.Contains("net5.0", args.RemainingArguments);
        }

        [Theory]
#pragma warning disable CA1825 // Avoid zero-length array allocations. https://github.com/dotnet/sdk/issues/28672
        [MemberData(nameof(CanEvaluateTemplateToRunData))]
#pragma warning restore CA1825 // Avoid zero-length array allocations.
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

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(defaultParameters: defaultParams);
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse($"new create {command}");
            var instantiateCommand = (InstantiateCommand)parseResult.CommandResult.Command;
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);
            HashSet<TemplateCommand> templateCommands = InstantiateCommand.GetTemplateCommand(args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Equal(expectedIdentities.Length, templateCommands.Count);
            Assert.Equal(expectedIdentities.OrderBy(s => s), templateCommands.Select(templateCommand => templateCommand.Template.Identity).OrderBy(s => s));
        }

        [Theory]
        [InlineData("new create foo --name name", "name")]
        [InlineData("new create foo -n name", "name")]
        [InlineData("new create foo", null)]
        [InlineData("new create --name name foo ", "name")]
        [InlineData("new create -n name foo", "name")]
        internal void Create_CanParseNameOption(string command, string? expectedValue)
        {
            var template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group");

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
                .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            RootCommand rootCommand = new();
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            rootCommand.Add(myCommand);

            ParseResult parseResult = rootCommand.Parse(command);

            var instantiateCommand = (InstantiateCommand)parseResult.CommandResult.Command;
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);
            TemplateCommand templateCommand = new(instantiateCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());
            Parser parser = ParserFactory.CreateParser(templateCommand);
            ParseResult templateParseResult = parser.Parse(args.TokensToInvoke ?? Array.Empty<string>());
            var templateArgs = new TemplateCommandArgs(templateCommand, instantiateCommand, templateParseResult);

            Assert.Equal(expectedValue, templateArgs.Name);
        }

        [Theory]
        [InlineData("new --name name create foo", "Unrecognized command or argument(s): '--name','name'.")]
        [InlineData("--name name new create foo", "Unrecognized command or argument '--name'.|Unrecognized command or argument 'name'.")]
        [InlineData("new --output name create foo", "Unrecognized command or argument(s): '--output','name'.")]
        [InlineData("new --project name create foo", "Unrecognized command or argument(s): '--project','name'.|File does not exist: 'name'.")]
        [InlineData("new --force create foo", "Unrecognized command or argument(s): '--force'.")]
        [InlineData("new --dry-run create foo", "Unrecognized command or argument(s): '--dry-run'.")]
        [InlineData("new --no-update-check create foo", "Unrecognized command or argument(s): '--no-update-check'.")]
        internal void Create_CanValidateOptionUsage_InNewCommand(string command, string? expectedErrors)
        {
            string[] expectedErrorsParsed = expectedErrors?.Split("|") ?? Array.Empty<string>();
            var template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group");

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
                .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            RootCommand rootCommand = new();
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            rootCommand.Add(myCommand);

            ParseResult parseResult = rootCommand.Parse(command);

            parseResult.Errors.Select(e => e.Message).Should().BeEquivalentTo(expectedErrorsParsed);
        }

        [Theory]
#pragma warning disable CA1825 // Avoid zero-length array allocations. https://github.com/dotnet/sdk/issues/28672
        [MemberData(nameof(CanParseTemplateOptionsData))]
#pragma warning restore CA1825 // Avoid zero-length array allocations.
        internal void Create_CanParseTemplateOptions(string command, string parameterName, string parameterType, string? defaultValue, string? defaultIfNoOptionValue, string? expectedValue)
        {
            //unique case for dotnet new create
            if (command == "foo -in 30")
            {
                command = "foo -i 30"; //for dotnet new create "-i" is not occupied, so we can use it.
            }

            MockTemplateInfo template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithParameter(parameterName, parameterType, defaultValue: defaultValue, defaultIfNoOptionValue: defaultIfNoOptionValue);

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
                .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse($"new create {command}");
            var instantiateCommand = (InstantiateCommand)parseResult.CommandResult.Command;
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);
            TemplateCommand templateCommand = new(instantiateCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());
            Parser parser = ParserFactory.CreateParser(templateCommand);
            ParseResult templateParseResult = parser.Parse(args.RemainingArguments ?? Array.Empty<string>());
            var templateArgs = new TemplateCommandArgs(templateCommand, instantiateCommand, templateParseResult);

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
#pragma warning disable CA1825 // Avoid zero-length array allocations. https://github.com/dotnet/sdk/issues/28672
        [MemberData(nameof(CanParseChoiceTemplateOptionsData))]
#pragma warning restore CA1825 // Avoid zero-length array allocations.
        internal void Create_CanParseChoiceTemplateOptions(string command, string parameterName, string parameterValues, string? defaultIfNoOptionValue, string? expectedValue)
        {
            MockTemplateInfo template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter(parameterName, parameterValues.Split("|"), defaultIfNoOptionValue: defaultIfNoOptionValue);

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
                .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse($"new create {command}");
            var instantiateCommand = (InstantiateCommand)parseResult.CommandResult.Command;
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);
            TemplateCommand templateCommand = new(instantiateCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());
            Parser parser = ParserFactory.CreateParser(templateCommand);
            ParseResult templateParseResult = parser.Parse(args.RemainingArguments ?? Array.Empty<string>());
            var templateArgs = new TemplateCommandArgs(templateCommand, instantiateCommand, templateParseResult);

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
#pragma warning disable CA1825 // Avoid zero-length array allocations. https://github.com/dotnet/sdk/issues/28672
        [MemberData(nameof(CanDetectParseErrorsTemplateOptionsData))]
#pragma warning restore CA1825 // Avoid zero-length array allocations.
        internal void Create_CanDetectParseErrorsTemplateOptions(
            string command,
            string parameterName,
            string parameterType,
            bool isRequired,
            string? defaultValue,
            string? defaultIfNoOptionValue,
            string expectedError)
        {
            MockTemplateInfo template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithParameter(parameterName, parameterType, isRequired, defaultValue: defaultValue, defaultIfNoOptionValue: defaultIfNoOptionValue);

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
                .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse($"new create {command}");
            var instantiateCommand = (InstantiateCommand)parseResult.CommandResult.Command;
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);

            TemplateCommand templateCommand = new(instantiateCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());
            Parser parser = ParserFactory.CreateParser(templateCommand);
            ParseResult templateParseResult = parser.Parse(args.RemainingArguments ?? Array.Empty<string>());
            Assert.True(templateParseResult.Errors.Any());
            Assert.Equal(expectedError, templateParseResult.Errors.Single().Message);
        }

        [Theory]
#pragma warning disable CA1825 // Avoid zero-length array allocations. https://github.com/dotnet/sdk/issues/28672
        [MemberData(nameof(CanDetectParseErrorsChoiceTemplateOptionsData))]
#pragma warning restore CA1825 // Avoid zero-length array allocations.
        internal void Create_CanDetectParseErrorsChoiceTemplateOptions(
              string command,
              string parameterName,
              string parameterValues,
              bool isRequired,
              string? defaultValue,
              string? defaultIfNoOptionValue,
              string expectedError)
        {
            MockTemplateInfo template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter(parameterName, parameterValues.Split("|"), isRequired, defaultValue, defaultIfNoOptionValue);

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
                .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse($" new create {command}");
            var instantiateCommand = (InstantiateCommand)parseResult.CommandResult.Command;
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);

            TemplateCommand templateCommand = new(instantiateCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());
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
            MockTemplateInfo template = new(command, identity: $"{command}Template");

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
                .Single();

            string[] expectedIdentities = expectedIdentitiesStr?.Split("|") ?? Array.Empty<string>();
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse($"new create {command}");
            var instantiateCommand = (InstantiateCommand)parseResult.CommandResult.Command;
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);
            HashSet<TemplateCommand> templateCommands = InstantiateCommand.GetTemplateCommand(args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Equal(expectedIdentities.Length, templateCommands.Count);
            Assert.Equal(expectedIdentities.OrderBy(s => s), templateCommands.Select(templateCommand => templateCommand.Template.Identity).OrderBy(s => s));
        }
    }
}
