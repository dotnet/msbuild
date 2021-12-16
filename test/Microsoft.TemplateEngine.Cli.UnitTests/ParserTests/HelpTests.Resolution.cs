// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Builder;
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
    [UsesVerify]
    public partial class HelpTests : IClassFixture<VerifyFixture>
    {
        private readonly VerifyFixture _verifySettings;

        public HelpTests(VerifyFixture verifySettings)
        {
            _verifySettings = verifySettings;
        }
    
        [Fact]
        public void UniqueNameMatchesCorrectly()
        {
            IReadOnlyList<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>()
                {
                    new MockTemplateInfo("console2", name: "Long name for Console App #2", identity: "Console.App2")
                };
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templatesToSearch, A.Fake<IHostSpecificDataLoader>())).Single();

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
            InstantiateCommand instantiateCommand = InstantiateCommand.FromNewCommand(myCommand);
            var parseResult = instantiateCommand.Parse($" new console2");
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);
            var matchingTemplates = InstantiateCommand.GetMatchingTemplates(instantiateCommand, args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Equal(1, matchingTemplates.Count());
            StringWriter output = new StringWriter();
            Assert.True(InstantiateCommand.VerifyMatchingTemplates(settings, output, matchingTemplates, out _));
            Assert.Empty(output.ToString());
        }

        [Fact]
        public Task FailedToResolveTemplate_WhenMultipleLanguagesAreFound()
        {
            IReadOnlyList<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>()
                    {
                        new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L1", groupIdentity: "Console.App.Test").WithTag("language", "L1"),
                        new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L2", groupIdentity: "Console.App.Test").WithTag("language", "L2"),
                        new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L3", groupIdentity: "Console.App.Test").WithTag("language", "L3")
                    };

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templatesToSearch, A.Fake<IHostSpecificDataLoader>())).Single();

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
            InstantiateCommand instantiateCommand = InstantiateCommand.FromNewCommand(myCommand);
            var parseResult = instantiateCommand.Parse($" new console");
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);
            var matchingTemplates = InstantiateCommand.GetMatchingTemplates(instantiateCommand, args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Equal(3, matchingTemplates.Count());
            StringWriter output = new StringWriter();
            Assert.False(InstantiateCommand.VerifyMatchingTemplates(settings, output, matchingTemplates, out _));
            return Verifier.Verify(output.ToString(), _verifySettings.Settings);
        }

        [Fact]
        public void DefaultLanguageDisambiguates()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L1", groupIdentity: "Console.App.Test").WithTag("language", "L1"));
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L2", groupIdentity: "Console.App.Test").WithTag("language", "L2"));

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templatesToSearch, A.Fake<IHostSpecificDataLoader>())).Single();

            var defaultParams = new Dictionary<string, string>();
            defaultParams["prefs:language"] = "L1";

            ITemplateEngineHost host = TestHost.GetVirtualHost(defaultParameters: defaultParams);
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
            InstantiateCommand instantiateCommand = InstantiateCommand.FromNewCommand(myCommand);
            var parseResult = instantiateCommand.Parse($" new console");
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);
            var matchingTemplates = InstantiateCommand.GetMatchingTemplates(instantiateCommand, args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Equal(2, matchingTemplates.Count());
            StringWriter output = new StringWriter();
            Assert.True(InstantiateCommand.VerifyMatchingTemplates(settings, output, matchingTemplates, out IEnumerable<TemplateCommand>? filtered));
            Assert.Equal(1, filtered?.Count());
            Assert.Equal("Console.App.L1", filtered?.Single().Template.Identity);
            Assert.Empty(output.ToString());
        }

        [Fact]
        public void InputLanguageIsPreferredOverDefault()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L1", groupIdentity: "Console.App.Test").WithTag("language", "L1"));
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L2", groupIdentity: "Console.App.Test").WithTag("language", "L2"));
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templatesToSearch, A.Fake<IHostSpecificDataLoader>())).Single();

            var defaultParams = new Dictionary<string, string>();
            defaultParams["prefs:language"] = "L1";

            ITemplateEngineHost host = TestHost.GetVirtualHost(defaultParameters: defaultParams);
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
            InstantiateCommand instantiateCommand = InstantiateCommand.FromNewCommand(myCommand);
            var parseResult = instantiateCommand.Parse($"new console --language L2");
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);
            var matchingTemplates = InstantiateCommand.GetMatchingTemplates(instantiateCommand, args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Equal(1, matchingTemplates.Count());
            StringWriter output = new StringWriter();
            Assert.True(InstantiateCommand.VerifyMatchingTemplates(settings, output, matchingTemplates, out IEnumerable<TemplateCommand>? filtered));
            Assert.Equal(1, filtered?.Count());
            Assert.Equal("Console.App.L2", filtered?.Single().Template.Identity);
            Assert.Empty(output.ToString());
        }

        [Fact]
        public void TemplatesAreSameLanguage()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test").WithTag("language", "L1"));
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T2", groupIdentity: "Console.App.Test").WithTag("language", "L1"));
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T3", groupIdentity: "Console.App.Test").WithTag("language", "L1"));
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templatesToSearch, A.Fake<IHostSpecificDataLoader>())).Single();

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
            InstantiateCommand instantiateCommand = InstantiateCommand.FromNewCommand(myCommand);
            var parseResult = instantiateCommand.Parse($"new console");
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);
            var matchingTemplates = InstantiateCommand.GetMatchingTemplates(instantiateCommand, args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Equal(3, matchingTemplates.Count());
            StringWriter output = new StringWriter();
            Assert.True(InstantiateCommand.VerifyMatchingTemplates(settings, output, matchingTemplates, out IEnumerable<TemplateCommand>? filtered));
            Assert.Equal(3, filtered?.Count());
            Assert.Empty(output.ToString());
        }

        [Fact]
        public void HasLanguageMismatch()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithBaselineInfo("app", "standard"));

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templatesToSearch, A.Fake<IHostSpecificDataLoader>())).Single();

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
            InstantiateCommand instantiateCommand = InstantiateCommand.FromNewCommand(myCommand);
            var parseResult = instantiateCommand.Parse($"new console --language L2");
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);
            var matchingTemplates = InstantiateCommand.GetMatchingTemplates(instantiateCommand, args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Equal(0, matchingTemplates.Count());
        }

        [Fact]
        public void HasTypeMismatch()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();

            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithBaselineInfo("app", "standard"));

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templatesToSearch, A.Fake<IHostSpecificDataLoader>())).Single();

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
            InstantiateCommand instantiateCommand = InstantiateCommand.FromNewCommand(myCommand);
            var parseResult = instantiateCommand.Parse($"new console --type item");
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);
            var matchingTemplates = InstantiateCommand.GetMatchingTemplates(instantiateCommand, args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Equal(0, matchingTemplates.Count());
        }

        [Fact]
        public void HasBaselineMismatch()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithBaselineInfo("app", "standard"));

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templatesToSearch, A.Fake<IHostSpecificDataLoader>())).Single();

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
            InstantiateCommand instantiateCommand = InstantiateCommand.FromNewCommand(myCommand);
            var parseResult = instantiateCommand.Parse($"new console --baseline core");
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);
            var matchingTemplates = InstantiateCommand.GetMatchingTemplates(instantiateCommand, args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Equal(0, matchingTemplates.Count());
        }

        [Fact]
        public void HasMultipleMismatches()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();

            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithBaselineInfo("app", "standard"));

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templatesToSearch, A.Fake<IHostSpecificDataLoader>())).Single();

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
            InstantiateCommand instantiateCommand = InstantiateCommand.FromNewCommand(myCommand);
            var parseResult = instantiateCommand.Parse($"new console --language L2 --type item --baseline core");
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);
            var matchingTemplates = InstantiateCommand.GetMatchingTemplates(instantiateCommand, args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Equal(0, matchingTemplates.Count());
        }

        [Fact]
        public void HasTypeMismatch_HasGroupLanguageMatch()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();

            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithBaselineInfo("app", "standard"));

            templatesToSearch.Add(
               new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T2", groupIdentity: "Console.App.Test")
                   .WithTag("language", "L2")
                   .WithTag("type", "project")
                   .WithBaselineInfo("app", "standard"));

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templatesToSearch, A.Fake<IHostSpecificDataLoader>())).Single();

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
            InstantiateCommand instantiateCommand = InstantiateCommand.FromNewCommand(myCommand);
            var parseResult = instantiateCommand.Parse($"new console --language L2 --type item");
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);
            var matchingTemplates = InstantiateCommand.GetMatchingTemplates(instantiateCommand, args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Equal(0, matchingTemplates.Count());
        }

        [Fact]
        public void OtherParameterMatch_Text()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();

            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithParameters("langVersion")
                    .WithBaselineInfo("app", "standard"));

            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T2", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithParameters("test")
                    .WithBaselineInfo("app", "standard"));

            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T3", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithBaselineInfo("app", "standard"));

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templatesToSearch, A.Fake<IHostSpecificDataLoader>())).Single();

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
            InstantiateCommand instantiateCommand = InstantiateCommand.FromNewCommand(myCommand);
            var parseResult = instantiateCommand.Parse($"new console --langVersion ver");
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);
            var matchingTemplates = InstantiateCommand.GetMatchingTemplates(instantiateCommand, args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Equal(1, matchingTemplates.Count());
        }

        [Fact]
        public void OtherParameterMatch_Choice()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();

            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithChoiceParameter("framework", "netcoreapp1.0", "netcoreapp1.1")
                    .WithBaselineInfo("app", "standard"));

            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T2", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithParameters("test")
                    .WithBaselineInfo("app", "standard"));

            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T3", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithBaselineInfo("app", "standard"));

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templatesToSearch, A.Fake<IHostSpecificDataLoader>())).Single();

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
            InstantiateCommand instantiateCommand = InstantiateCommand.FromNewCommand(myCommand);
            var parseResult = instantiateCommand.Parse($"new console --framework netcoreapp1.0");
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);
            var matchingTemplates = InstantiateCommand.GetMatchingTemplates(instantiateCommand, args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Equal(1, matchingTemplates.Count());

        }

        [Fact]
        public void OtherParameterDoesNotExist()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();

            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithChoiceParameter("framework", "netcoreapp1.0", "netcoreapp1.1")
                    .WithBaselineInfo("app", "standard"));

            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T2", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithParameters("test")
                    .WithBaselineInfo("app", "standard"));

            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T3", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithBaselineInfo("app", "standard"));

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templatesToSearch, A.Fake<IHostSpecificDataLoader>())).Single();

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());
            InstantiateCommand instantiateCommand = InstantiateCommand.FromNewCommand(myCommand);
            var parseResult = instantiateCommand.Parse($"new console --do-not-exist");
            var args = new InstantiateCommandArgs(instantiateCommand, parseResult);
            var matchingTemplates = InstantiateCommand.GetMatchingTemplates(instantiateCommand, args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Equal(0, matchingTemplates.Count());
        }
    }
}
