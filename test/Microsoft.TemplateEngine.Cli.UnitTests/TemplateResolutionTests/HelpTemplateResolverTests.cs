// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Cli.UnitTests.CliMocks;
using Microsoft.TemplateEngine.Mocks;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests.TemplateResolutionTests
{
    public class HelpTemplateResolverTests
    {
        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_UniqueNameMatchesCorrectly))]
        public async Task TestGetTemplateResolutionResult_UniqueNameMatchesCorrectly()
        {
            IReadOnlyList<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>()
            {
                new MockTemplateInfo("console1", name: "Long name for Console App", identity: "Console.App"),
                new MockTemplateInfo("console2", name: "Long name for Console App #2", identity: "Console.App2")
            };

            INewCommandInput userInputs = new MockNewCommandInput("console2").WithHelpOption();
            HelpTemplateResolver resolver = new HelpTemplateResolver(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(userInputs, defaultLanguage: null, default).ConfigureAwait(false);

            Assert.Equal(TemplateResolutionResult.Status.SingleMatch, matchResult.ResolutionStatus);
            Assert.Equal(TemplateResolutionResult.TemplateGroupStatus.SingleMatch, matchResult.GroupResolutionStatus);

            Assert.Equal("console2", matchResult.UnambiguousTemplateGroupMatchInfo?.TemplatesWithMatchingParametersForPreferredLanguage.Single().ShortNameList.Single());
            Assert.Equal("Console.App2", matchResult.UnambiguousTemplateGroupMatchInfo?.TemplatesWithMatchingParametersForPreferredLanguage.Single().Identity);
            Assert.Equal(1, matchResult.UnambiguousTemplateGroupMatchInfo?.TemplatesWithMatchingParametersForPreferredLanguage.Count());
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_ExactMatchOnShortNameMatchesCorrectly))]
        public async Task TestGetTemplateResolutionResult_ExactMatchOnShortNameMatchesCorrectly()
        {
            IReadOnlyList<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>()
            {
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App"),
                new MockTemplateInfo("console2", name: "Long name for Console App #2", identity: "Console.App2")
            };

            INewCommandInput userInputs = new MockNewCommandInput("console").WithHelpOption();
            HelpTemplateResolver resolver = new HelpTemplateResolver(templatesToSearch, new MockHostSpecificDataLoader());

            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(userInputs, defaultLanguage: null, default).ConfigureAwait(false);

            Assert.Equal(TemplateResolutionResult.Status.SingleMatch, matchResult.ResolutionStatus);
            Assert.Equal(TemplateResolutionResult.TemplateGroupStatus.SingleMatch, matchResult.GroupResolutionStatus);
            Assert.Equal("console", matchResult.UnambiguousTemplateGroupMatchInfo?.TemplatesWithMatchingParametersForPreferredLanguage.Single().ShortNameList.Single());
            Assert.Equal("Console.App", matchResult.UnambiguousTemplateGroupMatchInfo?.TemplatesWithMatchingParametersForPreferredLanguage.Single().Identity);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_UnambiguousGroupIsFound))]
        public async Task TestGetTemplateResolutionResult_UnambiguousGroupIsFound()
        {
            IReadOnlyList<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>()
            {
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L1", groupIdentity: "Console.App.Test").WithTag("language", "L1"),
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L2", groupIdentity: "Console.App.Test").WithTag("language", "L2"),
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L3", groupIdentity: "Console.App.Test").WithTag("language", "L3")
            };

            INewCommandInput userInputs = new MockNewCommandInput("console").WithHelpOption();
            HelpTemplateResolver resolver = new HelpTemplateResolver(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(userInputs, defaultLanguage: null, default).ConfigureAwait(false);

            Assert.Equal(TemplateResolutionResult.Status.AmbiguousLanguageChoice, matchResult.ResolutionStatus);
            Assert.Equal(TemplateResolutionResult.TemplateGroupStatus.SingleMatch, matchResult.GroupResolutionStatus);
            Assert.NotNull(matchResult.UnambiguousTemplateGroupMatchInfo);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_MultipleGroupsAreFound))]
        public async Task TestGetTemplateResolutionResult_MultipleGroupsAreFound()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L1", groupIdentity: "Console.App.Test").WithTag("language", "L1"));
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L2", groupIdentity: "Console.App.Test").WithTag("language", "L2"));
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L3", groupIdentity: "Console.App.Test").WithTag("language", "L3"));
            templatesToSearch.Add(new MockTemplateInfo("classlib", name: "Long name for Class Library App", identity: "Class.Library.L1", groupIdentity: "Class.Library.Test").WithTag("language", "L1"));
            templatesToSearch.Add(new MockTemplateInfo("classlib", name: "Long name for Class Library App", identity: "Class.Library.L2", groupIdentity: "Class.Library.Test").WithTag("language", "L2"));

            INewCommandInput userInputs = new MockNewCommandInput("c").WithHelpOption();

            HelpTemplateResolver resolver = new HelpTemplateResolver(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(userInputs, defaultLanguage: null, default).ConfigureAwait(false);
            Assert.Equal(TemplateResolutionResult.Status.NoMatch, matchResult.ResolutionStatus);
            Assert.Equal(TemplateResolutionResult.TemplateGroupStatus.NoMatch, matchResult.GroupResolutionStatus);
            Assert.Null(matchResult.UnambiguousTemplateGroupMatchInfo);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_DefaultLanguageDisambiguates))]
        public async Task TestGetTemplateResolutionResult_DefaultLanguageDisambiguates()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L1", groupIdentity: "Console.App.Test").WithTag("language", "L1"));
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L2", groupIdentity: "Console.App.Test").WithTag("language", "L2"));

            INewCommandInput userInputs = new MockNewCommandInput("console").WithHelpOption();

            HelpTemplateResolver resolver = new HelpTemplateResolver(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(userInputs, defaultLanguage: "L1", default).ConfigureAwait(false);

            Assert.Equal(TemplateResolutionResult.Status.SingleMatch, matchResult.ResolutionStatus);
            Assert.Equal(TemplateResolutionResult.TemplateGroupStatus.SingleMatch, matchResult.GroupResolutionStatus);
            Assert.Equal("console", matchResult.UnambiguousTemplateGroupMatchInfo?.TemplatesWithMatchingParametersForPreferredLanguage.Single().ShortNameList.Single());
            Assert.Equal("Console.App.L1", matchResult.UnambiguousTemplateGroupMatchInfo?.TemplatesWithMatchingParametersForPreferredLanguage.Single().Identity);
            Assert.Equal(1, matchResult.UnambiguousTemplateGroupMatchInfo?.TemplatesWithMatchingParametersForPreferredLanguage.Count());
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_InputLanguageIsPreferredOverDefault))]
        public async Task TestGetTemplateResolutionResult_InputLanguageIsPreferredOverDefault()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L1", groupIdentity: "Console.App.Test").WithTag("language", "L1"));
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L2", groupIdentity: "Console.App.Test").WithTag("language", "L2"));

            INewCommandInput userInputs = new MockNewCommandInput("console", "L2").WithHelpOption();
            HelpTemplateResolver resolver = new HelpTemplateResolver(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(userInputs, defaultLanguage: "L1", default).ConfigureAwait(false);

            Assert.Equal(TemplateResolutionResult.Status.SingleMatch, matchResult.ResolutionStatus);
            Assert.Equal(TemplateResolutionResult.TemplateGroupStatus.SingleMatch, matchResult.GroupResolutionStatus);
            Assert.Equal("console", matchResult.UnambiguousTemplateGroupMatchInfo?.TemplatesWithMatchingParametersForPreferredLanguage.Single().ShortNameList.Single());
            Assert.Equal("Console.App.L2", matchResult.UnambiguousTemplateGroupMatchInfo?.TemplatesWithMatchingParametersForPreferredLanguage.Single().Identity);
            Assert.Equal("L2", matchResult.UnambiguousTemplateGroupMatchInfo?.TemplatesWithMatchingParametersForPreferredLanguage.Single().TagsCollection["language"]);
            Assert.Equal(1, matchResult.UnambiguousTemplateGroupMatchInfo?.TemplatesWithMatchingParametersForPreferredLanguage.Count());
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_UnambiguousGroup_TemplatesAreNotSameLanguage))]
        public async Task TestGetTemplateResolutionResult_UnambiguousGroup_TemplatesAreNotSameLanguage()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L1", groupIdentity: "Console.App.Test").WithTag("language", "L1"));
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L2", groupIdentity: "Console.App.Test").WithTag("language", "L2"));
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L3", groupIdentity: "Console.App.Test").WithTag("language", "L3"));

            INewCommandInput userInputs = new MockNewCommandInput("console").WithHelpOption();
            HelpTemplateResolver resolver = new HelpTemplateResolver(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(userInputs, defaultLanguage: null, default).ConfigureAwait(false);
            Assert.Equal(TemplateResolutionResult.Status.AmbiguousLanguageChoice, matchResult.ResolutionStatus);
            Assert.Equal(TemplateResolutionResult.TemplateGroupStatus.SingleMatch, matchResult.GroupResolutionStatus);
            Assert.NotNull(matchResult.UnambiguousTemplateGroupMatchInfo);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_UnambiguousGroup_TemplatesAreSameLanguage))]
        public async Task TestGetTemplateResolutionResult_UnambiguousGroup_TemplatesAreSameLanguage()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test").WithTag("language", "L1"));
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T2", groupIdentity: "Console.App.Test").WithTag("language", "L1"));
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T3", groupIdentity: "Console.App.Test").WithTag("language", "L1"));

            INewCommandInput userInputs = new MockNewCommandInput("console").WithHelpOption();
            HelpTemplateResolver resolver = new HelpTemplateResolver(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(userInputs, defaultLanguage: null, default).ConfigureAwait(false);
            Assert.Equal(TemplateResolutionResult.TemplateGroupStatus.SingleMatch, matchResult.GroupResolutionStatus);
            Assert.Equal(3, matchResult.UnambiguousTemplateGroupMatchInfo?.TemplatesWithMatchingParametersForPreferredLanguage.Count());
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_PartialMatch_HasLanguageMismatch))]
        public async Task TestGetTemplateResolutionResult_PartialMatch_HasLanguageMismatch()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithBaselineInfo("app", "standard"));

            INewCommandInput userInputs = new MockNewCommandInput("console", "L2").WithHelpOption();

            HelpTemplateResolver resolver = new HelpTemplateResolver(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(userInputs, defaultLanguage: null, default).ConfigureAwait(false);
            Assert.Equal(TemplateResolutionResult.TemplateGroupStatus.NoMatch, matchResult.GroupResolutionStatus);
            Assert.Null(matchResult.UnambiguousTemplateGroupMatchInfo);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_PartialMatch_HasContextMismatch))]
        public async Task TestGetTemplateResolutionResult_PartialMatch_HasContextMismatch()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();

            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithBaselineInfo("app", "standard"));

            INewCommandInput userInputs = new MockNewCommandInput("console", type: "item").WithHelpOption();
            HelpTemplateResolver resolver = new HelpTemplateResolver(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(userInputs, defaultLanguage: null, default).ConfigureAwait(false);
            Assert.Equal(TemplateResolutionResult.TemplateGroupStatus.NoMatch, matchResult.GroupResolutionStatus);
            Assert.Null(matchResult.UnambiguousTemplateGroupMatchInfo);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_PartialMatch_HasBaselineMismatch))]
        public async Task TestGetTemplateResolutionResult_PartialMatch_HasBaselineMismatch()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithBaselineInfo("app", "standard"));

            INewCommandInput userInputs = new MockNewCommandInput("console").WithHelpOption().WithCommandOption("--baseline", "core");
            HelpTemplateResolver resolver = new HelpTemplateResolver(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(userInputs, defaultLanguage: null, default).ConfigureAwait(false);
            Assert.Equal(TemplateResolutionResult.TemplateGroupStatus.NoMatch, matchResult.GroupResolutionStatus);
            Assert.Null(matchResult.UnambiguousTemplateGroupMatchInfo);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_PartialMatch_HasMultipleMismatches))]
        public async Task TestGetTemplateResolutionResult_PartialMatch_HasMultipleMismatches()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();

            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithBaselineInfo("app", "standard"));

            INewCommandInput userInputs = new MockNewCommandInput("console", "L2", "item").WithHelpOption().WithCommandOption("--baseline", "core");
            HelpTemplateResolver resolver = new HelpTemplateResolver(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(userInputs, defaultLanguage: null, default).ConfigureAwait(false);
            Assert.Equal(TemplateResolutionResult.TemplateGroupStatus.NoMatch, matchResult.GroupResolutionStatus);
            Assert.Null(matchResult.UnambiguousTemplateGroupMatchInfo);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_PartialMatchGroup_HasTypeMismatch_HasGroupLanguageMatch))]
        public async Task TestGetTemplateResolutionResult_PartialMatchGroup_HasTypeMismatch_HasGroupLanguageMatch()
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

            INewCommandInput userInputs = new MockNewCommandInput("console", "L2", "item").WithHelpOption();
            HelpTemplateResolver resolver = new HelpTemplateResolver(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(userInputs, defaultLanguage: null, default).ConfigureAwait(false);
            Assert.Equal(TemplateResolutionResult.TemplateGroupStatus.NoMatch, matchResult.GroupResolutionStatus);
            Assert.Null(matchResult.UnambiguousTemplateGroupMatchInfo);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_NoMatch))]
        public async Task TestGetTemplateResolutionResult_NoMatch()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();

            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithBaselineInfo("app", "standard"));

            INewCommandInput userInputs = new MockNewCommandInput("zzzzz", "L1", "item").WithHelpOption().WithCommandOption("--baseline", "app");
            HelpTemplateResolver resolver = new HelpTemplateResolver(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(userInputs, defaultLanguage: null, default).ConfigureAwait(false);
            Assert.Equal(TemplateResolutionResult.TemplateGroupStatus.NoMatch, matchResult.GroupResolutionStatus);
            Assert.Null(matchResult.UnambiguousTemplateGroupMatchInfo);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_OtherParameterMatch_Text))]
        public async Task TestGetTemplateResolutionResult_OtherParameterMatch_Text()
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

            INewCommandInput userInputs = new MockNewCommandInput("console").WithHelpOption().WithTemplateOption("langVersion");

            HelpTemplateResolver resolver = new HelpTemplateResolver(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(userInputs, defaultLanguage: null, default).ConfigureAwait(false);
            Assert.Equal(TemplateResolutionResult.TemplateGroupStatus.SingleMatch, matchResult.GroupResolutionStatus);
            Assert.Equal(1, matchResult.UnambiguousTemplateGroupMatchInfo?.TemplatesWithMatchingParametersForPreferredLanguage.Count());
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_OtherParameterMatch_Choice))]
        public async Task TestGetTemplateResolutionResult_OtherParameterMatch_Choice()
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

            INewCommandInput userInputs = new MockNewCommandInput("console").WithHelpOption().WithTemplateOption("framework", "netcoreapp1.0");
            HelpTemplateResolver resolver = new HelpTemplateResolver(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(userInputs, defaultLanguage: null, default).ConfigureAwait(false);
            Assert.Equal(TemplateResolutionResult.TemplateGroupStatus.SingleMatch, matchResult.GroupResolutionStatus);
            Assert.Equal(1, matchResult.UnambiguousTemplateGroupMatchInfo?.TemplatesWithMatchingParametersForPreferredLanguage.Count());

            //specifying choice value without choice is not allowed
            userInputs = new MockNewCommandInput("console").WithHelpOption().WithTemplateOption("framework");
            matchResult = await resolver.ResolveTemplatesAsync(userInputs, defaultLanguage: null, default).ConfigureAwait(false);
            Assert.Equal(TemplateResolutionResult.TemplateGroupStatus.SingleMatch, matchResult.GroupResolutionStatus);
            Assert.Equal(0, matchResult.UnambiguousTemplateGroupMatchInfo?.TemplatesWithMatchingParametersForPreferredLanguage.Count());

        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_OtherParameterDoesNotExist))]
        public async Task TestGetTemplateResolutionResult_OtherParameterDoesNotExist()
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

            INewCommandInput userInputs = new MockNewCommandInput("console").WithHelpOption().WithTemplateOption("do-not-exist");

            HelpTemplateResolver resolver = new HelpTemplateResolver(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(userInputs, defaultLanguage: null, default).ConfigureAwait(false);
            Assert.Equal(TemplateResolutionResult.TemplateGroupStatus.SingleMatch, matchResult.GroupResolutionStatus);
            Assert.Equal(0, matchResult.UnambiguousTemplateGroupMatchInfo?.TemplatesWithMatchingParametersForPreferredLanguage.Count());
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_MatchByTagsIgnoredForHelp))]
        public async Task TestGetTemplateResolutionResult_MatchByTagsIgnoredForHelp()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test1")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithClassifications("Common", "Test")
                    .WithBaselineInfo("app", "standard"));

            INewCommandInput userInputs = new MockNewCommandInput("Common").WithHelpOption();
            HelpTemplateResolver resolver = new HelpTemplateResolver(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(userInputs, defaultLanguage: null, default).ConfigureAwait(false);
            Assert.Equal(TemplateResolutionResult.TemplateGroupStatus.NoMatch, matchResult.GroupResolutionStatus);
            Assert.Null(matchResult.UnambiguousTemplateGroupMatchInfo);
        }
    }
}
