// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    public partial class TabCompletionTests
    {
        [Fact]
        public void Instantiate_CanSuggestTemplateOption_StartsWith()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse("new console --framework net5.0 --l");
            var suggestions = parseResult.GetSuggestions().ToArray();

            Assert.Equal(2, suggestions.Length);
            Assert.Contains("--langVersion", suggestions);
            Assert.Contains("--language", suggestions);
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact (Skip = "doesn't work at the moment; it matches with legacy --language option which cannot be completed; to discuss how to avoid that")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void Instantiate_CanSuggestLanguages()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse("new console --language ");
            var suggestions = parseResult.GetSuggestions().ToArray();

            Assert.Equal(3, suggestions.Length);
            Assert.Contains("C#", suggestions);
            Assert.Contains("F#", suggestions);
            Assert.Contains("VB", suggestions);
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "not valid behavior for parser, should suggest --nuget-source")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void Install_GetSuggestionsAfterInteractive()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse("new install --interactive ");
            var result = parseResult.GetSuggestions().ToArray();

            Assert.Equal(2, result.Length);
            Assert.Contains("--nuget-source", result);
        }

        [Fact]
        public void Install_GetSuggestionsAfterOptionWithoutArg()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse("new install --nuget-source ");
            var result = parseResult.GetSuggestions().ToArray();

            Assert.Empty(result);
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "not valid behavior for parser, should suggest --interactive probably")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void Install_GetSuggestionsAfterOptionWithArg()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse("new install --nuget-source me");
            var result = parseResult.GetSuggestions().ToArray();

            Assert.Equal(1, result.Length);
            Assert.Contains("--interactive", result);
        }

        [Fact]
        public void Instantiate_CanSuggestTemplate_StartsWith()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse("new co");
            var suggestions = parseResult.GetSuggestions().ToArray();

            Assert.Single(suggestions);
            Assert.Equal("console", suggestions.Single());
        }

        [Fact]
        public void CanCompleteChoice_FromSingleTemplate()
        {
            var template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val1", "val2", "val3");

            var templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()));

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
            InstantiateCommand instantiateCommand = InstantiateCommand.FromNewCommand(myCommand);
            var parseResult = instantiateCommand.Parse($" new foo --testChoice ");
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);

            var result = instantiateCommand.GetSuggestions(args, templateGroups, settings, packageManager, "");

            Assert.Equal(new[] { "val1", "val2", "val3" }, result);
        }

        [Fact]
        public void CanCompleteChoice_FromSingleTemplate_StartsWith()
        {
            var template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val1", "val2", "boo");

            var templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()));

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
            InstantiateCommand instantiateCommand = InstantiateCommand.FromNewCommand(myCommand);
            var parseResult = instantiateCommand.Parse($" new foo --testChoice v");
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);

            var result = instantiateCommand.GetSuggestions(args, templateGroups, settings, packageManager, "");

            Assert.Equal(new[] { "val1", "val2" }, result);
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact (Skip = "not working, text to match is not considered")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void CanCompleteChoice_FromSingleTemplate_InTheMiddle()
        {
            var template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val1", "val2", "boo");

            var templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()));

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
            InstantiateCommand instantiateCommand = InstantiateCommand.FromNewCommand(myCommand);
            var parseResult = instantiateCommand.Parse($" new foo --testChoice v --name test");
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);

            var result = instantiateCommand.GetSuggestions(args, templateGroups, settings, packageManager, "v");

            Assert.Equal(new[] { "val1", "val2" }, result);
        }

        [Fact]
        public void CanCompleteChoice_FromMultipleTemplates()
        {
            var template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val1");

            var template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val2", "val3");

            var templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template1, template2 }, A.Fake<IHostSpecificDataLoader>()));

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
            InstantiateCommand instantiateCommand = InstantiateCommand.FromNewCommand(myCommand);
            var parseResult = instantiateCommand.Parse($" new foo --testChoice ");
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);

            var result = instantiateCommand.GetSuggestions(args, templateGroups, settings, packageManager, "");

            Assert.Equal(new[] { "val1", "val2", "val3" }, result);
        }

        [Fact]
        public void CanCompleteChoice_FromMultipleTemplates_StartsWith()
        {
            var template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val1");

            var template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val2", "boo");

            var templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template1, template2 }, A.Fake<IHostSpecificDataLoader>()));

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
            InstantiateCommand instantiateCommand = InstantiateCommand.FromNewCommand(myCommand);
            var parseResult = instantiateCommand.Parse($" new foo --testChoice v");
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);

            var result = instantiateCommand.GetSuggestions(args, templateGroups, settings, packageManager, "");

            Assert.Equal(new[] { "val1", "val2" }, result);
        }

        [Fact]
        public void CanCompleteParameters_FromMultipleTemplates()
        {
            var template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val1")
                .WithParameters("foo", "bar");

            var template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val2", "val3")
                .WithParameters("param");

            var templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template1, template2 }, A.Fake<IHostSpecificDataLoader>()));

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
            InstantiateCommand instantiateCommand = InstantiateCommand.FromNewCommand(myCommand);
            var parseResult = instantiateCommand.Parse($" new foo ");
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);

            var result = instantiateCommand.GetSuggestions(args, templateGroups, settings, packageManager, "");

            Assert.Contains("--param", result);
            Assert.Contains("--testChoice", result);
            Assert.Contains("--foo", result);
            Assert.Contains("--bar", result);

            Assert.Contains("-p", result);
            Assert.Contains("-t", result);
            Assert.Contains("-f", result);
            Assert.Contains("-b", result);

            Assert.DoesNotContain("--language", result);
            Assert.DoesNotContain("--type", result);
            Assert.DoesNotContain("--baseline", result);
        }

        [Theory]
        [InlineData("-lang")]
        [InlineData("--language")]
        public void CanCompleteLanguages(string optionName)
        {
            var template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithTag("language", "C#");

            var template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group")
                .WithTag("language", "F#");

            var templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template1, template2 }, A.Fake<IHostSpecificDataLoader>()));

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
            InstantiateCommand instantiateCommand = InstantiateCommand.FromNewCommand(myCommand);
            var parseResult = instantiateCommand.Parse($" new foo {optionName} ");
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);

            var result = instantiateCommand.GetSuggestions(args, templateGroups, settings, packageManager, "");

            Assert.Equal(new[] { "C#", "F#" }, result);
        }

        [Fact]
        public void CanCompleteTypes()
        {
            var template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithTag("type", "project");

            var template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group")
                .WithTag("type", "solution");

            var templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template1, template2 }, A.Fake<IHostSpecificDataLoader>()));

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
            InstantiateCommand instantiateCommand = InstantiateCommand.FromNewCommand(myCommand);
            var parseResult = instantiateCommand.Parse($" new foo --type ");
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);

            var result = instantiateCommand.GetSuggestions(args, templateGroups, settings, packageManager, "");

            Assert.Equal(new[] { "project", "solution" }, result);
        }
    }
}
