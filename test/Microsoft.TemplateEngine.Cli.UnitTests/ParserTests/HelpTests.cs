// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
using VerifyXunit;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    public partial class HelpTests
    {
        [Theory]
#pragma warning disable SA1117 // Parameters should be on same line or separate lines
        [InlineData("Template Name", "Language", "Me", "Template Description",
@"Template Name (Language)
Author: Me
Description: Template Description

")]
        [InlineData("Template Name", null, "Me", "Template Description",
@"Template Name
Author: Me
Description: Template Description

")]
        [InlineData("Template Name", "Language", null, "Template Description",
@"Template Name (Language)
Description: Template Description

")]
        [InlineData("Template Name", "Language", "Me", null,
@"Template Name (Language)
Author: Me

")]
        [InlineData("Template Name", null, null, null,
@"Template Name

")]
#pragma warning restore SA1117 // Parameters should be on same line or separate lines
        public void CanShowTemplateDescription(string name, string? language, string? author, string? description, string expected)
        {
            MockTemplateInfo templateInfo = new MockTemplateInfo(
                "console2",
                name: name,
                identity: "Console.App2",
                author: author);

            templateInfo.WithDescription(description);
            if (language != null)
            {
                templateInfo.WithTag("language", language);
            }

            CliTemplateInfo cliTemplateInfo = new CliTemplateInfo(templateInfo, HostSpecificTemplateData.Default);
            StringWriter sw = new StringWriter();
            InstantiateCommand.ShowTemplateDetailHeaders(cliTemplateInfo, sw);
            Assert.Equal(expected, sw.ToString());
        }

        [Fact]
        public void CanShowUsage()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false), new NewCommandCallbacks());
            StringWriter sw = new StringWriter();
            HelpContext helpContext = new HelpContext(new HelpBuilder(LocalizationResources.Instance), myCommand, sw);

            InstantiateCommand.ShowUsage(myCommand, new[] { "short-name" }, helpContext);
            Assert.Equal($"Usage:{Environment.NewLine}  new short-name [options] [template options]{Environment.NewLine}{Environment.NewLine}", sw.ToString());
        }

        [Fact]
        public void CanShowUsage_ForMultipleShortNames()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false), new NewCommandCallbacks());
            StringWriter sw = new StringWriter();
            HelpContext helpContext = new HelpContext(new HelpBuilder(LocalizationResources.Instance), myCommand, sw);

            InstantiateCommand.ShowUsage(myCommand, new[] { "short-name1", "short-name2" }, helpContext);
            Assert.Equal($"Usage:{Environment.NewLine}  new short-name1 [options] [template options]{Environment.NewLine}  new short-name2 [options] [template options]{Environment.NewLine}{Environment.NewLine}", sw.ToString());
        }

        [Fact]
        public Task CanShowCommandOptions_Basic()
        {
            var template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group");
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
               CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
               .Single();

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false), new NewCommandCallbacks());

            TemplateCommand templateCommand = new TemplateCommand(myCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());

            StringWriter sw = new StringWriter();
            HelpContext helpContext = new HelpContext(new HelpBuilder(LocalizationResources.Instance), myCommand, sw);

            InstantiateCommand.ShowCommandOptions(new[] { templateCommand }, templateCommand, helpContext);
            return Verifier.Verify(sw.ToString(), _verifySettings.Settings);
        }

        [Fact]
        public Task CanShowCommandOptions_Language()
        {
            var template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group").WithTag("language", "MyLang");
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
               CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
               .Single();

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false), new NewCommandCallbacks());

            TemplateCommand templateCommand = new TemplateCommand(myCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());

            StringWriter sw = new StringWriter();
            HelpContext helpContext = new HelpContext(new HelpBuilder(LocalizationResources.Instance), myCommand, sw);

            InstantiateCommand.ShowCommandOptions(new[] { templateCommand }, templateCommand, helpContext);
            return Verifier.Verify(sw.ToString(), _verifySettings.Settings);
        }

        [Fact]
        public Task CanShowCommandOptions_Type()
        {
            var template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group").WithTag("type", "MyType");
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
               CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
               .Single();

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false), new NewCommandCallbacks());

            TemplateCommand templateCommand = new TemplateCommand(myCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());

            StringWriter sw = new StringWriter();
            HelpContext helpContext = new HelpContext(new HelpBuilder(LocalizationResources.Instance), myCommand, sw);

            InstantiateCommand.ShowCommandOptions(new[] { templateCommand }, templateCommand, helpContext);
            return Verifier.Verify(sw.ToString(), _verifySettings.Settings);
        }

        [Fact]
        public void CanShowCommandOptions_NoOptions()
        {
            var template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group").WithTag("type", "MyType");
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
               CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
               .Single();

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false), new NewCommandCallbacks());

            TemplateCommand templateCommand = new TemplateCommand(myCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());

            StringWriter sw = new StringWriter();
            HelpContext helpContext = new HelpContext(new HelpBuilder(LocalizationResources.Instance), myCommand, sw);

            InstantiateCommand.ShowTemplateSpecificOptions(new[] { templateCommand }, helpContext);
            Assert.Equal($"Template options:{Environment.NewLine}   (No options){Environment.NewLine}", sw.ToString());
        }

        [Fact]
        public Task CanShowTemplateOptions_SingleTemplate_Choice()
        {
            var template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("choice", new[] { "val1", "val2" }, description: "my description", defaultValue: "def-val", defaultIfNoOptionValue: "def-val-no-arg" );
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
               CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
               .Single();

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false), new NewCommandCallbacks());

            TemplateCommand templateCommand = new TemplateCommand(myCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());

            StringWriter sw = new StringWriter();
            HelpContext helpContext = new HelpContext(new HelpBuilder(LocalizationResources.Instance), myCommand, sw);

            InstantiateCommand.ShowTemplateSpecificOptions(new[] { templateCommand }, helpContext);
            return Verifier.Verify(sw.ToString(), _verifySettings.Settings);
        }

        [Fact]
        public Task CanShowTemplateOptions_MultipleTemplate_CombinedChoice()
        {
            var template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group", precedence: 0)
                .WithChoiceParameter("choice", new[] { "val1", "val2" }, description: "my description", defaultValue: "def-val-not-shown", defaultIfNoOptionValue: "def-val-not-shown");
            var template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group", precedence: 2)
             .WithChoiceParameter("choice", new[] { "val1", "val3" }, description: "my description", defaultValue: "def-val", defaultIfNoOptionValue: "def-val-no-arg");
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
               CliTemplateInfo.FromTemplateInfo(new[] { template1, template2 }, A.Fake<IHostSpecificDataLoader>()))
               .Single();

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false), new NewCommandCallbacks());

            TemplateCommand templateCommand1 = new TemplateCommand(myCommand, settings, packageManager, templateGroup, templateGroup.Templates[0]);
            TemplateCommand templateCommand2 = new TemplateCommand(myCommand, settings, packageManager, templateGroup, templateGroup.Templates[1]);

            StringWriter sw = new StringWriter();
            HelpContext helpContext = new HelpContext(new HelpBuilder(LocalizationResources.Instance), myCommand, sw);

            InstantiateCommand.ShowTemplateSpecificOptions(new[] { templateCommand2, templateCommand1 }, helpContext);
            return Verifier.Verify(sw.ToString(), _verifySettings.Settings);
        }

        [Fact]
        public Task CanShowTemplateOptions_SingleTemplate_NonChoice()
        {
            var template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithParameter("non-choice", paramType: "text", description: "my description", defaultValue: "def-val", defaultIfNoOptionValue: "def-val-no-arg");
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
               CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
               .Single();

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false), new NewCommandCallbacks());

            TemplateCommand templateCommand = new TemplateCommand(myCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());

            StringWriter sw = new StringWriter();
            HelpContext helpContext = new HelpContext(new HelpBuilder(LocalizationResources.Instance), myCommand, sw);

            InstantiateCommand.ShowTemplateSpecificOptions(new[] { templateCommand }, helpContext);
            return Verifier.Verify(sw.ToString(), _verifySettings.Settings);
        }

        [Fact]
        public Task CanShowTemplateOptions_MultipleTemplate_MultipleParams()
        {
            var template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group", precedence: 0)
                .WithChoiceParameter("choice", new[] { "val1", "val2" }, description: "my description", defaultValue: "def-val-not-shown", defaultIfNoOptionValue: "def-val-not-shown")
                .WithParameter("bool", paramType: "boolean", description: "my bool", defaultValue: "false", defaultIfNoOptionValue: "false");
            var template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group", precedence: 2)
             .WithChoiceParameter("choice", new[] { "val1", "val3" }, description: "my description", defaultValue: "def-val", defaultIfNoOptionValue: "def-val-no-arg")
             .WithParameter("int", paramType: "integer", description: "my int", defaultValue: "0", defaultIfNoOptionValue: "10");
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
               CliTemplateInfo.FromTemplateInfo(new[] { template1, template2 }, A.Fake<IHostSpecificDataLoader>()))
               .Single();

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false), new NewCommandCallbacks());

            TemplateCommand templateCommand1 = new TemplateCommand(myCommand, settings, packageManager, templateGroup, templateGroup.Templates[0]);
            TemplateCommand templateCommand2 = new TemplateCommand(myCommand, settings, packageManager, templateGroup, templateGroup.Templates[1]);

            StringWriter sw = new StringWriter();
            HelpContext helpContext = new HelpContext(new HelpBuilder(LocalizationResources.Instance), myCommand, sw);

            InstantiateCommand.ShowTemplateSpecificOptions(new[] { templateCommand2, templateCommand1 }, helpContext);
            return Verifier.Verify(sw.ToString(), _verifySettings.Settings);
        }

        [Fact]
        public Task CanShowTemplateOptions_SingleTemplate_Choice_Required()
        {
            var template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("choice", new[] { "val1", "val2" }, description: "my description", defaultIfNoOptionValue: "def-val-no-arg", isRequired: true);
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
               CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
               .Single();

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false), new NewCommandCallbacks());

            TemplateCommand templateCommand = new TemplateCommand(myCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());

            StringWriter sw = new StringWriter();
            HelpContext helpContext = new HelpContext(new HelpBuilder(LocalizationResources.Instance), myCommand, sw);

            InstantiateCommand.ShowTemplateSpecificOptions(new[] { templateCommand }, helpContext);
            return Verifier.Verify(sw.ToString(), _verifySettings.Settings);
        }

        [Fact]
        public void CanShowTemplateOptions_RequiredIsNotShownWhenDefaultValueIsGiven()
        {
            var template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("choice", new[] { "val1", "val2" }, description: "my description", defaultValue: "def-val", defaultIfNoOptionValue: "def-val-no-arg", isRequired: true);
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
               CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
               .Single();

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false), new NewCommandCallbacks());

            TemplateCommand templateCommand = new TemplateCommand(myCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());

            StringWriter sw = new StringWriter();
            HelpContext helpContext = new HelpContext(new HelpBuilder(LocalizationResources.Instance), myCommand, sw);

            InstantiateCommand.ShowTemplateSpecificOptions(new[] { templateCommand }, helpContext);
            Assert.DoesNotContain("(REQUIRED)", sw.ToString());
        }

        [Fact]
        public Task CanShowHintsForOtherTemplates()
        {
            var template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group").WithTag("language", "Lang1").WithTag("type", "project");
            var template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group").WithTag("language", "Lang2").WithTag("type", "item");

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
               CliTemplateInfo.FromTemplateInfo(new[] { template1, template2 }, A.Fake<IHostSpecificDataLoader>()))
               .Single();

            ITemplateEngineHost host = TestHost.GetVirtualHost();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false), new NewCommandCallbacks());
            ParseResult parseResult = myCommand.Parse("new -h");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));

            StringWriter sw = new StringWriter();

            InstantiateCommand.ShowHintForOtherTemplates(templateGroup, templateGroup.Templates[0], args, sw);
            return Verifier.Verify(sw.ToString(), _verifySettings.Settings);
        }

        [Fact]
        public Task CanShowTemplateOptions_SingleTemplate_Choice_ShortenedUsage_FirstTwoValuesFit()
        {
            var template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("choice", new[] { "val1", "val2", "val3", "val4", "val5", "val6" }, description: "my description", defaultValue: "def-val", defaultIfNoOptionValue: "def-val-no-arg");
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
               CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
               .Single();

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false), new NewCommandCallbacks());

            TemplateCommand templateCommand = new TemplateCommand(myCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());

            StringWriter sw = new StringWriter();
            HelpContext helpContext = new HelpContext(new HelpBuilder(LocalizationResources.Instance, maxWidth: 100), myCommand, sw);

            InstantiateCommand.ShowTemplateSpecificOptions(new[] { templateCommand }, helpContext);
            return Verifier.Verify(sw.ToString(), _verifySettings.Settings);
        }

        [Fact]
        public Task CanShowTemplateOptions_SingleTemplate_Choice_ShortenedUsage()
        {
            var template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("choice", new[] { "val1", "val2", "val3", "val4", "val5", "val6" }, description: "my description", defaultValue: "def-val", defaultIfNoOptionValue: "def-val-no-arg");
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
               CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
               .Single();

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false), new NewCommandCallbacks());

            TemplateCommand templateCommand = new TemplateCommand(myCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());

            StringWriter sw = new StringWriter();
            HelpContext helpContext = new HelpContext(new HelpBuilder(LocalizationResources.Instance, maxWidth: 50), myCommand, sw);

            InstantiateCommand.ShowTemplateSpecificOptions(new[] { templateCommand }, helpContext);
            return Verifier.Verify(sw.ToString(), _verifySettings.Settings);
        }

    }
}
