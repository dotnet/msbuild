// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Help;
using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Mocks;

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
            MockTemplateInfo templateInfo = new(
                "console2",
                name: name,
                identity: "Console.App2",
                author: author);

            templateInfo.WithDescription(description);
            if (language != null)
            {
                templateInfo.WithTag("language", language);
            }

            CliTemplateInfo cliTemplateInfo = new(templateInfo, HostSpecificTemplateData.Default);
            StringWriter sw = new();
            InstantiateCommand.ShowTemplateDetailHeaders(cliTemplateInfo, sw);
            Assert.Equal(expected, sw.ToString());
        }

        [Fact]
        public void CanShowUsage()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            StringWriter sw = new();
            HelpContext helpContext = new(new HelpBuilder(), myCommand, sw);

            InstantiateCommand.ShowUsage(myCommand, new[] { "short-name" }, helpContext);
            Assert.Equal($"Usage:{Environment.NewLine}  new short-name [options] [template options]{Environment.NewLine}{Environment.NewLine}", sw.ToString());
        }

        [Fact]
        public void CanShowUsage_ForMultipleShortNames()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            StringWriter sw = new();
            HelpContext helpContext = new(new HelpBuilder(), myCommand, sw);

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

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            TemplateCommand templateCommand = new(myCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());

            StringWriter sw = new();
            HelpContext helpContext = new(new HelpBuilder(), myCommand, sw);

            InstantiateCommand.ShowCommandOptions(new[] { templateCommand }, helpContext);
            return Verify(sw.ToString());
        }

        [Fact]
        public Task CanShowCommandOptions_Language()
        {
            MockTemplateInfo template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group").WithTag("language", "MyLang");
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
               CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
               .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            TemplateCommand templateCommand = new(myCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());

            StringWriter sw = new();
            HelpContext helpContext = new(new HelpBuilder(), myCommand, sw);

            InstantiateCommand.ShowCommandOptions(new[] { templateCommand }, helpContext);
            return Verify(sw.ToString());
        }

        [Fact]
        public Task CanShowCommandOptions_Type()
        {
            MockTemplateInfo template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group").WithTag("type", "MyType");
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
               CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
               .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            TemplateCommand templateCommand = new(myCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());

            StringWriter sw = new();
            HelpContext helpContext = new(new HelpBuilder(), myCommand, sw);

            InstantiateCommand.ShowCommandOptions(new[] { templateCommand }, helpContext);
            return Verify(sw.ToString());
        }

        [Fact]
        public void CanShowCommandOptions_NoOptions()
        {
            MockTemplateInfo template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group").WithTag("type", "MyType");
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
               CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
               .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            TemplateCommand templateCommand = new(myCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());

            StringWriter sw = new();
            HelpContext helpContext = new(new HelpBuilder(), myCommand, sw);

            InstantiateCommand.ShowTemplateSpecificOptions(new[] { templateCommand }, helpContext);
            Assert.Equal($"Template options:{Environment.NewLine}   (No options){Environment.NewLine}", sw.ToString());
        }

        [Fact]
        public Task CanShowTemplateOptions_SingleTemplate_Choice()
        {
            MockTemplateInfo template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("choice", new[] { "val1", "val2" }, description: "my description", defaultValue: "def-val", defaultIfNoOptionValue: "def-val-no-arg");
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
               CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
               .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            TemplateCommand templateCommand = new(myCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());

            StringWriter sw = new();
            HelpContext helpContext = new(new HelpBuilder(), myCommand, sw);

            InstantiateCommand.ShowTemplateSpecificOptions(new[] { templateCommand }, helpContext);
            return Verify(sw.ToString());
        }

        [Fact]
        public Task CanShowTemplateOptions_MultipleTemplate_CombinedChoice()
        {
            MockTemplateInfo template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group", precedence: 0)
                .WithChoiceParameter("choice", new[] { "val1", "val2" }, description: "my description", defaultValue: "def-val-not-shown", defaultIfNoOptionValue: "def-val-not-shown");
            MockTemplateInfo template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group", precedence: 2)
             .WithChoiceParameter("choice", new[] { "val1", "val3" }, description: "my description", defaultValue: "def-val", defaultIfNoOptionValue: "def-val-no-arg");
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
               CliTemplateInfo.FromTemplateInfo(new[] { template1, template2 }, A.Fake<IHostSpecificDataLoader>()))
               .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            TemplateCommand templateCommand1 = new(myCommand, settings, packageManager, templateGroup, templateGroup.Templates[0]);
            TemplateCommand templateCommand2 = new(myCommand, settings, packageManager, templateGroup, templateGroup.Templates[1]);

            StringWriter sw = new();
            HelpContext helpContext = new(new HelpBuilder(), myCommand, sw);

            InstantiateCommand.ShowTemplateSpecificOptions(new[] { templateCommand2, templateCommand1 }, helpContext);
            return Verify(sw.ToString());
        }

        [Fact]
        public Task CanShowTemplateOptions_SingleTemplate_NonChoice()
        {
            MockTemplateInfo template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithParameter("non-choice", paramType: "text", description: "my description", defaultValue: "def-val", defaultIfNoOptionValue: "def-val-no-arg");
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
               CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
               .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            TemplateCommand templateCommand = new(myCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());

            StringWriter sw = new();
            HelpContext helpContext = new(new HelpBuilder(), myCommand, sw);

            InstantiateCommand.ShowTemplateSpecificOptions(new[] { templateCommand }, helpContext);
            return Verify(sw.ToString());
        }

        [Fact]
        public Task CanShowTemplateOptions_MultipleTemplate_MultipleParams()
        {
            MockTemplateInfo template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group", precedence: 0)
                .WithChoiceParameter("choice", new[] { "val1", "val2" }, description: "my description", defaultValue: "def-val-not-shown", defaultIfNoOptionValue: "def-val-not-shown")
                .WithParameter("bool", paramType: "boolean", description: "my bool", defaultValue: "false", defaultIfNoOptionValue: "false");
            MockTemplateInfo template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group", precedence: 2)
             .WithChoiceParameter("choice", new[] { "val1", "val3" }, description: "my description", defaultValue: "def-val", defaultIfNoOptionValue: "def-val-no-arg")
             .WithParameter("int", paramType: "integer", description: "my int", defaultValue: "0", defaultIfNoOptionValue: "10");
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
               CliTemplateInfo.FromTemplateInfo(new[] { template1, template2 }, A.Fake<IHostSpecificDataLoader>()))
               .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            TemplateCommand templateCommand1 = new(myCommand, settings, packageManager, templateGroup, templateGroup.Templates[0]);
            TemplateCommand templateCommand2 = new(myCommand, settings, packageManager, templateGroup, templateGroup.Templates[1]);

            StringWriter sw = new();
            HelpContext helpContext = new(new HelpBuilder(), myCommand, sw);

            InstantiateCommand.ShowTemplateSpecificOptions(new[] { templateCommand2, templateCommand1 }, helpContext);
            return Verify(sw.ToString());
        }

        [Fact]
        public Task CanShowTemplateOptions_SingleTemplate_Choice_Required()
        {
            MockTemplateInfo template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("choice", new[] { "val1", "val2" }, description: "my description", defaultIfNoOptionValue: "def-val-no-arg", isRequired: true);
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
               CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
               .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            TemplateCommand templateCommand = new(myCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());

            StringWriter sw = new();
            HelpContext helpContext = new(new HelpBuilder(), myCommand, sw);

            InstantiateCommand.ShowTemplateSpecificOptions(new[] { templateCommand }, helpContext);
            return Verify(sw.ToString());
        }

        [Fact]
        public void CanShowTemplateOptions_RequiredIsNotShownWhenDefaultValueIsGiven()
        {
            MockTemplateInfo template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("choice", new[] { "val1", "val2" }, description: "my description", defaultValue: "def-val", defaultIfNoOptionValue: "def-val-no-arg", isRequired: true);
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
               CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
               .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            TemplateCommand templateCommand = new(myCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());

            StringWriter sw = new();
            HelpContext helpContext = new(new HelpBuilder(), myCommand, sw);

            InstantiateCommand.ShowTemplateSpecificOptions(new[] { templateCommand }, helpContext);
            Assert.DoesNotContain("(REQUIRED)", sw.ToString());
        }

        [Fact]
        public Task CanShowHintsForOtherTemplates()
        {
            MockTemplateInfo template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group").WithTag("language", "Lang1").WithTag("type", "project");
            MockTemplateInfo template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group").WithTag("language", "Lang2").WithTag("type", "item");

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
               CliTemplateInfo.FromTemplateInfo(new[] { template1, template2 }, A.Fake<IHostSpecificDataLoader>()))
               .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse("new -h");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));

            StringWriter sw = new();

            InstantiateCommand.ShowHintForOtherTemplates(templateGroup, templateGroup.Templates[0], args, sw);
            return Verify(sw.ToString());
        }

        [Fact]
        public Task CanShowTemplateOptions_SingleTemplate_Choice_ShortenedUsage_FirstTwoValuesFit()
        {
            MockTemplateInfo template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("choice", new[] { "val1", "val2", "val3", "val4", "val5", "val6" }, description: "my description", defaultValue: "def-val", defaultIfNoOptionValue: "def-val-no-arg");
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
               CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
               .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            TemplateCommand templateCommand = new(myCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());

            StringWriter sw = new();
            HelpContext helpContext = new(new HelpBuilder(maxWidth: 100), myCommand, sw);

            InstantiateCommand.ShowTemplateSpecificOptions(new[] { templateCommand }, helpContext);
            return Verify(sw.ToString());
        }

        [Fact]
        public Task CanShowTemplateOptions_SingleTemplate_Choice_ShortenedUsage()
        {
            MockTemplateInfo template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("choice", new[] { "val1", "val2", "val3", "val4", "val5", "val6" }, description: "my description", defaultValue: "def-val", defaultIfNoOptionValue: "def-val-no-arg");
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
               CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
               .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            TemplateCommand templateCommand = new(myCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());

            StringWriter sw = new();
            HelpContext helpContext = new(new HelpBuilder(maxWidth: 50), myCommand, sw);

            InstantiateCommand.ShowTemplateSpecificOptions(new[] { templateCommand }, helpContext);
            return Verify(sw.ToString());
        }

        [Fact]
        public Task DoesNotCombineParametersWhenAliasesAreDifferent()
        {
            MockTemplateInfo template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("Choice", new[] { "val1" }, description: "my description", defaultValue: "def-val", defaultIfNoOptionValue: "def-val-no-arg");

            MockTemplateInfo template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group")
                .WithChoiceParameter("Choice", new[] { "val2" }, description: "my description", defaultValue: "def-val", defaultIfNoOptionValue: "def-val-no-arg");

            IHostSpecificDataLoader hostDataLoader = A.Fake<IHostSpecificDataLoader>();
            A.CallTo(() => hostDataLoader.ReadHostSpecificTemplateData(template2))
                .Returns(new HostSpecificTemplateData(
                    new Dictionary<string, IReadOnlyDictionary<string, string>>()
                    {
                        {
                            "Choice", new Dictionary<string, string>()
                            {
                                { "longName", "choice" },
                                { "shortName", "C" }
                            }
                        }
                    }));

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
               CliTemplateInfo.FromTemplateInfo(new[] { template1, template2 }, hostDataLoader))
               .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            TemplateCommand templateCommand1 = new(myCommand, settings, packageManager, templateGroup, templateGroup.Templates[0]);
            TemplateCommand templateCommand2 = new(myCommand, settings, packageManager, templateGroup, templateGroup.Templates[1]);

            StringWriter sw = new();
            HelpContext helpContext = new(new HelpBuilder(maxWidth: 50), myCommand, sw);

            InstantiateCommand.ShowTemplateSpecificOptions(new[] { templateCommand1, templateCommand2 }, helpContext);
            return Verify(sw.ToString());
        }

        [Fact]
        public Task CanShowTemplateOptions_RequiredParam()
        {
            MockTemplateInfo template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("choice", new[] { "val1", "val2", "val3", "val4", "val5", "val6" }, description: "my description", isRequired: true);
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
               CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()))
               .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            TemplateCommand templateCommand = new(myCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single());

            StringWriter sw = new();
            HelpContext helpContext = new(new HelpBuilder(maxWidth: 50), myCommand, sw);

            InstantiateCommand.ShowTemplateSpecificOptions(new[] { templateCommand }, helpContext);
            return Verify(sw.ToString());
        }
    }
}
