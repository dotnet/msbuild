// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Mocks;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    [UsesVerify]
    public partial class HelpTests
    {
        [Fact]
        public void UniqueNameMatchesCorrectly()
        {
            IReadOnlyList<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>()
                {
                    new MockTemplateInfo("console2", name: "Long name for Console App #2", identity: "Console.App2")
                };
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templatesToSearch, A.Fake<IHostSpecificDataLoader>())).Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse($" new console2");
            var args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            IEnumerable<TemplateCommand> matchingTemplates = InstantiateCommand.GetMatchingTemplates(args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Single(matchingTemplates);
            BufferedReporter reporter = new();
            Assert.True(InstantiateCommand.VerifyMatchingTemplates(settings, matchingTemplates, reporter, out _));
            Assert.Empty(reporter.Lines);
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

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse($" new console");
            var args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            IEnumerable<TemplateCommand> matchingTemplates = InstantiateCommand.GetMatchingTemplates(args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Equal(3, matchingTemplates.Count());
            StringWriter output = new();
            BufferedReporter reporter = new();
            Assert.False(InstantiateCommand.VerifyMatchingTemplates(settings, matchingTemplates, reporter, out _));
            return Verify(string.Join(Environment.NewLine, reporter.Lines));
        }

        [Fact]
        public void DefaultLanguageDisambiguates()
        {
            List<ITemplateInfo> templatesToSearch = new()
            {
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L1", groupIdentity: "Console.App.Test").WithTag("language", "L1"),
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L2", groupIdentity: "Console.App.Test").WithTag("language", "L2")
            };

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templatesToSearch, A.Fake<IHostSpecificDataLoader>())).Single();

            var defaultParams = new Dictionary<string, string>
            {
                ["prefs:language"] = "L1"
            };

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(defaultParameters: defaultParams);
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse($" new console");
            var args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            IEnumerable<TemplateCommand> matchingTemplates = InstantiateCommand.GetMatchingTemplates(args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Equal(2, matchingTemplates.Count());
            BufferedReporter reporter = new();
            Assert.True(InstantiateCommand.VerifyMatchingTemplates(settings, matchingTemplates, reporter, out IEnumerable<TemplateCommand>? filtered));
            Assert.Equal(1, filtered?.Count());
            Assert.Equal("Console.App.L1", filtered?.Single().Template.Identity);
            Assert.Empty(reporter.Lines);
        }

        [Fact]
        public void InputLanguageIsPreferredOverDefault()
        {
            List<ITemplateInfo> templatesToSearch = new()
            {
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L1", groupIdentity: "Console.App.Test").WithTag("language", "L1"),
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L2", groupIdentity: "Console.App.Test").WithTag("language", "L2")
            };
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templatesToSearch, A.Fake<IHostSpecificDataLoader>())).Single();

            var defaultParams = new Dictionary<string, string>
            {
                ["prefs:language"] = "L1"
            };

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(defaultParameters: defaultParams);
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse($"new console --language L2");
            var args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            IEnumerable<TemplateCommand> matchingTemplates = InstantiateCommand.GetMatchingTemplates(args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Single(matchingTemplates);
            BufferedReporter reporter = new();
            Assert.True(InstantiateCommand.VerifyMatchingTemplates(settings, matchingTemplates, reporter, out IEnumerable<TemplateCommand>? filtered));
            Assert.Equal(1, filtered?.Count());
            Assert.Equal("Console.App.L2", filtered?.Single().Template.Identity);
            Assert.Empty(reporter.Lines);
        }

        [Fact]
        public void TemplatesAreSameLanguage()
        {
            List<ITemplateInfo> templatesToSearch = new()
            {
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test").WithTag("language", "L1"),
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T2", groupIdentity: "Console.App.Test").WithTag("language", "L1"),
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T3", groupIdentity: "Console.App.Test").WithTag("language", "L1")
            };
            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templatesToSearch, A.Fake<IHostSpecificDataLoader>())).Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse($"new console");
            var args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            IEnumerable<TemplateCommand> matchingTemplates = InstantiateCommand.GetMatchingTemplates(args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Equal(3, matchingTemplates.Count());
            BufferedReporter reporter = new();
            Assert.True(InstantiateCommand.VerifyMatchingTemplates(settings, matchingTemplates, reporter, out IEnumerable<TemplateCommand>? filtered));
            Assert.Equal(3, filtered?.Count());
            Assert.Empty(reporter.Lines);
        }

        [Fact]
        public void HasLanguageMismatch()
        {
            List<ITemplateInfo> templatesToSearch = new()
            {
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithBaselineInfo("app", "standard")
            };

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templatesToSearch, A.Fake<IHostSpecificDataLoader>())).Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse($"new console --language L2");
            var args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            IEnumerable<TemplateCommand> matchingTemplates = InstantiateCommand.GetMatchingTemplates(args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Empty(matchingTemplates);
        }

        [Fact]
        public void HasTypeMismatch()
        {
            List<ITemplateInfo> templatesToSearch = new()
            {
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithBaselineInfo("app", "standard")
            };

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templatesToSearch, A.Fake<IHostSpecificDataLoader>())).Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse($"new console --type item");
            var args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            IEnumerable<TemplateCommand> matchingTemplates = InstantiateCommand.GetMatchingTemplates(args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Empty(matchingTemplates);
        }

        [Fact]
        public void HasBaselineMismatch()
        {
            List<ITemplateInfo> templatesToSearch = new()
            {
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithBaselineInfo("app", "standard")
            };

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templatesToSearch, A.Fake<IHostSpecificDataLoader>())).Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse($"new console --baseline core");
            var args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            IEnumerable<TemplateCommand> matchingTemplates = InstantiateCommand.GetMatchingTemplates(args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Empty(matchingTemplates);
        }

        [Fact]
        public void HasMultipleMismatches()
        {
            List<ITemplateInfo> templatesToSearch = new()
            {
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithBaselineInfo("app", "standard")
            };

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templatesToSearch, A.Fake<IHostSpecificDataLoader>())).Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse($"new console --language L2 --type item --baseline core");
            var args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            IEnumerable<TemplateCommand> matchingTemplates = InstantiateCommand.GetMatchingTemplates(args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Empty(matchingTemplates);
        }

        [Fact]
        public void HasTypeMismatch_HasGroupLanguageMatch()
        {
            List<ITemplateInfo> templatesToSearch = new()
            {
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithBaselineInfo("app", "standard"),

                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T2", groupIdentity: "Console.App.Test")
                   .WithTag("language", "L2")
                   .WithTag("type", "project")
                   .WithBaselineInfo("app", "standard")
            };

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templatesToSearch, A.Fake<IHostSpecificDataLoader>())).Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse($"new console --language L2 --type item");
            var args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            IEnumerable<TemplateCommand> matchingTemplates = InstantiateCommand.GetMatchingTemplates(args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Empty(matchingTemplates);
        }

        [Fact]
        public void OtherParameterMatch_Text()
        {
            List<ITemplateInfo> templatesToSearch = new()
            {
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithParameters("langVersion")
                    .WithBaselineInfo("app", "standard"),

                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T2", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithParameters("test")
                    .WithBaselineInfo("app", "standard"),

                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T3", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithBaselineInfo("app", "standard")
            };

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templatesToSearch, A.Fake<IHostSpecificDataLoader>())).Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse($"new console --langVersion ver");
            var args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            IEnumerable<TemplateCommand> matchingTemplates = InstantiateCommand.GetMatchingTemplates(args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Single(matchingTemplates);
        }

        [Fact]
        public void OtherParameterMatch_Choice()
        {
            List<ITemplateInfo> templatesToSearch = new()
            {
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithChoiceParameter("framework", "netcoreapp1.0", "netcoreapp1.1")
                    .WithBaselineInfo("app", "standard"),

                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T2", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithParameters("test")
                    .WithBaselineInfo("app", "standard"),

                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T3", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithBaselineInfo("app", "standard")
            };

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templatesToSearch, A.Fake<IHostSpecificDataLoader>())).Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse($"new console --framework netcoreapp1.0");
            var args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            IEnumerable<TemplateCommand> matchingTemplates = InstantiateCommand.GetMatchingTemplates(args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Single(matchingTemplates);

        }

        [Fact]
        public void OtherParameterDoesNotExist()
        {
            List<ITemplateInfo> templatesToSearch = new()
            {
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithChoiceParameter("framework", "netcoreapp1.0", "netcoreapp1.1")
                    .WithBaselineInfo("app", "standard"),

                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T2", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithParameters("test")
                    .WithBaselineInfo("app", "standard"),

                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T3", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithBaselineInfo("app", "standard")
            };

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templatesToSearch, A.Fake<IHostSpecificDataLoader>())).Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse($"new console --do-not-exist");
            var args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            IEnumerable<TemplateCommand> matchingTemplates = InstantiateCommand.GetMatchingTemplates(args, settings, A.Fake<TemplatePackageManager>(), templateGroup);
            Assert.Empty(matchingTemplates);
        }
    }
}
