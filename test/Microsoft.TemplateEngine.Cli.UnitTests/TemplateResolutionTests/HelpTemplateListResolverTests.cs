// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Cli.HelpAndUsage;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Cli.UnitTests.CliMocks;
using Microsoft.TemplateEngine.Mocks;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests.TemplateResolutionTests
{
    public class HelpTemplateListResolverTests
    {
        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_UniqueNameMatchesCorrectly))]
        public void TestGetTemplateResolutionResult_UniqueNameMatchesCorrectly()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("console1", name: "Long name for Console App", identity: "Console.App"));
            templatesToSearch.Add(new MockTemplateInfo("console2", name: "Long name for Console App #2", identity: "Console.App2"));

            INewCommandInput userInputs = new MockNewCommandInput("console2").WithHelpOption();

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.Equal(TemplateResolutionResult.Status.SingleMatch, matchResult.ResolutionStatus);
            Assert.Equal(TemplateResolutionResult.UnambiguousTemplateGroupStatus.SingleMatch, matchResult.GroupResolutionStatus);

            Assert.Equal("console2", matchResult.TemplatesForDetailedHelp.Single().Info.ShortNameList.Single());
            Assert.Equal("Console.App2", matchResult.TemplatesForDetailedHelp.Single().Info.Identity);
            Assert.Equal(1, matchResult.TemplatesForDetailedHelp.Count());
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_ExactMatchOnShortNameMatchesCorrectly))]
        public void TestGetTemplateResolutionResult_ExactMatchOnShortNameMatchesCorrectly()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App"));
            templatesToSearch.Add(new MockTemplateInfo("console2", name: "Long name for Console App #2", identity: "Console.App2"));

            INewCommandInput userInputs = new MockNewCommandInput("console").WithHelpOption();

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.Equal(TemplateResolutionResult.Status.SingleMatch, matchResult.ResolutionStatus);
            Assert.Equal(TemplateResolutionResult.UnambiguousTemplateGroupStatus.SingleMatch, matchResult.GroupResolutionStatus);
            Assert.Equal("console", matchResult.TemplatesForDetailedHelp.Single().Info.ShortNameList.Single());
            Assert.Equal("Console.App", matchResult.TemplatesForDetailedHelp.Single().Info.Identity);
            Assert.Equal(1, matchResult.TemplatesForDetailedHelp.Count());
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_UnambiguousGroupIsFound))]
        public void TestGetTemplateResolutionResult_UnambiguousGroupIsFound()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L1", groupIdentity: "Console.App.Test").WithTag("language", "L1"));
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L2", groupIdentity: "Console.App.Test").WithTag("language", "L2"));
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L3", groupIdentity: "Console.App.Test").WithTag("language", "L3"));

            INewCommandInput userInputs = new MockNewCommandInput("console").WithHelpOption();

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);

            Assert.Equal(TemplateResolutionResult.Status.AmbiguousLanguageChoice, matchResult.ResolutionStatus);
            Assert.Equal(TemplateResolutionResult.UnambiguousTemplateGroupStatus.SingleMatch, matchResult.GroupResolutionStatus);
            Assert.Empty(matchResult.TemplatesForDetailedHelp); //ambiguous language
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_MultipleGroupsAreFound))]
        public void TestGetTemplateResolutionResult_MultipleGroupsAreFound()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L1", groupIdentity: "Console.App.Test").WithTag("language", "L1"));
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L2", groupIdentity: "Console.App.Test").WithTag("language", "L2"));
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L3", groupIdentity: "Console.App.Test").WithTag("language", "L3"));
            templatesToSearch.Add(new MockTemplateInfo("classlib", name: "Long name for Class Library App", identity: "Class.Library.L1", groupIdentity: "Class.Library.Test").WithTag("language", "L1"));
            templatesToSearch.Add(new MockTemplateInfo("classlib", name: "Long name for Class Library App", identity: "Class.Library.L2", groupIdentity: "Class.Library.Test").WithTag("language", "L2"));

            INewCommandInput userInputs = new MockNewCommandInput("c").WithHelpOption();

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.Equal(TemplateResolutionResult.Status.NoMatch, matchResult.ResolutionStatus);
            Assert.Equal(TemplateResolutionResult.UnambiguousTemplateGroupStatus.NoMatch, matchResult.GroupResolutionStatus);
            Assert.Empty(matchResult.TemplatesForDetailedHelp); //no partial matches allowed
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_DefaultLanguageDisambiguates))]
        public void TestGetTemplateResolutionResult_DefaultLanguageDisambiguates()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L1", groupIdentity: "Console.App.Test").WithTag("language", "L1"));
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L2", groupIdentity: "Console.App.Test").WithTag("language", "L2"));

            INewCommandInput userInputs = new MockNewCommandInput("console").WithHelpOption();

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, "L1");

            Assert.Equal(TemplateResolutionResult.Status.SingleMatch, matchResult.ResolutionStatus);
            Assert.Equal(TemplateResolutionResult.UnambiguousTemplateGroupStatus.SingleMatch, matchResult.GroupResolutionStatus);
            Assert.Equal("console", matchResult.TemplatesForDetailedHelp.Single().Info.ShortNameList.Single());
            Assert.Equal("Console.App.L1", matchResult.TemplatesForDetailedHelp.Single().Info.Identity);
            Assert.Equal(1, matchResult.TemplatesForDetailedHelp.Count());
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_InputLanguageIsPreferredOverDefault))]
        public void TestGetTemplateResolutionResult_InputLanguageIsPreferredOverDefault()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L1", groupIdentity: "Console.App.Test").WithTag("language", "L1"));
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L2", groupIdentity: "Console.App.Test").WithTag("language", "L2"));

            INewCommandInput userInputs = new MockNewCommandInput("console", "L2").WithHelpOption();

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, "L1");

            Assert.Equal(TemplateResolutionResult.Status.SingleMatch, matchResult.ResolutionStatus);
            Assert.Equal(TemplateResolutionResult.UnambiguousTemplateGroupStatus.SingleMatch, matchResult.GroupResolutionStatus);
            Assert.Equal("console", matchResult.TemplatesForDetailedHelp.Single().Info.ShortNameList.Single());
            Assert.Equal("Console.App.L2", matchResult.TemplatesForDetailedHelp.Single().Info.Identity);
            Assert.Equal("L2", matchResult.TemplatesForDetailedHelp.Single().Info.TagsCollection["language"]);
            Assert.Equal(1, matchResult.TemplatesForDetailedHelp.Count());
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_UnambiguousGroup_TemplatesAreNotSameLanguage))]
        public void TestGetTemplateResolutionResult_UnambiguousGroup_TemplatesAreNotSameLanguage()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L1", groupIdentity: "Console.App.Test").WithTag("language", "L1"));
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L2", groupIdentity: "Console.App.Test").WithTag("language", "L2"));
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L3", groupIdentity: "Console.App.Test").WithTag("language", "L3"));

            INewCommandInput userInputs = new MockNewCommandInput("console").WithHelpOption();

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.Equal(TemplateResolutionResult.Status.AmbiguousLanguageChoice, matchResult.ResolutionStatus);
            Assert.Equal(TemplateResolutionResult.UnambiguousTemplateGroupStatus.SingleMatch, matchResult.GroupResolutionStatus);
            Assert.Equal(0, matchResult.TemplatesForDetailedHelp.Count());
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_UnambiguousGroup_TemplatesAreSameLanguage))]
        public void TestGetTemplateResolutionResult_UnambiguousGroup_TemplatesAreSameLanguage()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test").WithTag("language", "L1"));
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T2", groupIdentity: "Console.App.Test").WithTag("language", "L1"));
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T3", groupIdentity: "Console.App.Test").WithTag("language", "L1"));

            INewCommandInput userInputs = new MockNewCommandInput("console").WithHelpOption();

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.Equal(TemplateResolutionResult.UnambiguousTemplateGroupStatus.SingleMatch, matchResult.GroupResolutionStatus);
            Assert.Equal(3, matchResult.TemplatesForDetailedHelp.Count());
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_PartialMatch_HasLanguageMismatch))]
        public void TestGetTemplateResolutionResult_PartialMatch_HasLanguageMismatch()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithBaselineInfo("app", "standard"));

            INewCommandInput userInputs = new MockNewCommandInput("console", "L2").WithHelpOption();

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.Equal(TemplateResolutionResult.UnambiguousTemplateGroupStatus.NoMatch, matchResult.GroupResolutionStatus);
            Assert.Empty(matchResult.TemplatesForDetailedHelp);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_PartialMatch_HasContextMismatch))]
        public void TestGetTemplateResolutionResult_PartialMatch_HasContextMismatch()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();

            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithBaselineInfo("app", "standard"));

            INewCommandInput userInputs = new MockNewCommandInput("console", type: "item").WithHelpOption();

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.Equal(TemplateResolutionResult.UnambiguousTemplateGroupStatus.NoMatch, matchResult.GroupResolutionStatus);
            Assert.Empty(matchResult.TemplatesForDetailedHelp);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_PartialMatch_HasBaselineMismatch))]
        public void TestGetTemplateResolutionResult_PartialMatch_HasBaselineMismatch()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithBaselineInfo("app", "standard"));

            INewCommandInput userInputs = new MockNewCommandInput("console").WithHelpOption().WithCommandOption("--baseline", "core");

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.Equal(TemplateResolutionResult.UnambiguousTemplateGroupStatus.NoMatch, matchResult.GroupResolutionStatus);
            Assert.Empty(matchResult.TemplatesForDetailedHelp);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_PartialMatch_HasMultipleMismatches))]
        public void TestGetTemplateResolutionResult_PartialMatch_HasMultipleMismatches()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();

            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithBaselineInfo("app", "standard"));

            INewCommandInput userInputs = new MockNewCommandInput("console", "L2", "item").WithHelpOption().WithCommandOption("--baseline", "core");

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.Equal(TemplateResolutionResult.UnambiguousTemplateGroupStatus.NoMatch, matchResult.GroupResolutionStatus);
            Assert.Empty(matchResult.TemplatesForDetailedHelp);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_PartialMatchGroup_HasTypeMismatch_HasGroupLanguageMatch))]
        public void TestGetTemplateResolutionResult_PartialMatchGroup_HasTypeMismatch_HasGroupLanguageMatch()
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

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.Equal(TemplateResolutionResult.UnambiguousTemplateGroupStatus.NoMatch, matchResult.GroupResolutionStatus);
            Assert.Empty(matchResult.TemplatesForDetailedHelp);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_NoMatch))]
        public void TestGetTemplateResolutionResult_NoMatch()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();

            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithBaselineInfo("app", "standard"));

            INewCommandInput userInputs = new MockNewCommandInput("zzzzz", "L1", "item").WithHelpOption().WithCommandOption("--baseline", "app");

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.Equal(TemplateResolutionResult.UnambiguousTemplateGroupStatus.NoMatch, matchResult.GroupResolutionStatus);
            Assert.Empty(matchResult.TemplatesForDetailedHelp);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_OtherParameterMatch_Text))]
        public void TestGetTemplateResolutionResult_OtherParameterMatch_Text()
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

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.Equal(TemplateResolutionResult.UnambiguousTemplateGroupStatus.SingleMatch, matchResult.GroupResolutionStatus);
            Assert.Equal(1, matchResult.TemplatesForDetailedHelp.Count());
            TemplateInformationCoordinator.GetParametersInvalidForTemplatesInList(matchResult.TemplatesForDetailedHelp.ToList(), out IReadOnlyList<string> invalidForAllTemplates, out IReadOnlyList<string> invalidForSomeTemplates);
            Assert.Equal(0, invalidForSomeTemplates.Count);
            Assert.Equal(0, invalidForAllTemplates.Count);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_OtherParameterMatch_Choice))]
        public void TestGetTemplateResolutionResult_OtherParameterMatch_Choice()
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

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.Equal(TemplateResolutionResult.UnambiguousTemplateGroupStatus.SingleMatch, matchResult.GroupResolutionStatus);
            Assert.Equal(1, matchResult.TemplatesForDetailedHelp.Count());

            //specifying choice value without choice is not allowed
            userInputs = new MockNewCommandInput("console").WithHelpOption().WithTemplateOption("framework");
            matchResult = TemplateResolver.GetTemplateResolutionResultForHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.Equal(TemplateResolutionResult.UnambiguousTemplateGroupStatus.SingleMatch, matchResult.GroupResolutionStatus);
            Assert.Equal(0, matchResult.TemplatesForDetailedHelp.Count());

        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_OtherParameterDoesNotExist))]
        public void TestGetTemplateResolutionResult_OtherParameterDoesNotExist()
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

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.Equal(TemplateResolutionResult.UnambiguousTemplateGroupStatus.SingleMatch, matchResult.GroupResolutionStatus);
            Assert.Equal(0, matchResult.TemplatesForDetailedHelp.Count());
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_MatchByTagsIgnoredForHelp))]
        public void TestGetTemplateResolutionResult_MatchByTagsIgnoredForHelp()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test1")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithClassifications("Common", "Test")
                    .WithBaselineInfo("app", "standard"));

            INewCommandInput userInputs = new MockNewCommandInput("Common").WithHelpOption();

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.Equal(TemplateResolutionResult.UnambiguousTemplateGroupStatus.NoMatch, matchResult.GroupResolutionStatus);
            Assert.Equal(0, matchResult.TemplatesForDetailedHelp.Count());
        }
    }
}
