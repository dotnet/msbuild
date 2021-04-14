// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Xunit;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Cli.UnitTests.CliMocks;
using System.Linq;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects;
using Microsoft.TemplateEngine.Cli.HelpAndUsage;
using Microsoft.TemplateEngine.Mocks;

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

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.True(matchResult.HasExactMatches);
            Assert.True(matchResult.HasUnambiguousTemplateGroup);
            Assert.Equal("console2", matchResult.UnambiguousTemplateGroup.Single().Info.ShortNameList.Single());
            Assert.Equal("Console.App2", matchResult.UnambiguousTemplateGroup.Single().Info.Identity);
            Assert.Equal(1, matchResult.UnambiguousTemplateGroup.Count);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_ExactMatchOnShortNameMatchesCorrectly))]
        public void TestGetTemplateResolutionResult_ExactMatchOnShortNameMatchesCorrectly()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App"));
            templatesToSearch.Add(new MockTemplateInfo("console2", name: "Long name for Console App #2", identity: "Console.App2"));

            INewCommandInput userInputs = new MockNewCommandInput("console").WithHelpOption();

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.True(matchResult.HasExactMatches);
            Assert.True(matchResult.HasUnambiguousTemplateGroup);
            Assert.Equal("console", matchResult.UnambiguousTemplateGroup.Single().Info.ShortNameList.Single());
            Assert.Equal("Console.App", matchResult.UnambiguousTemplateGroup.Single().Info.Identity);
            Assert.Equal(1, matchResult.UnambiguousTemplateGroup.Count);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_UnambiguousGroupIsFound))]
        public void TestGetTemplateResolutionResult_UnambiguousGroupIsFound()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L1", groupIdentity: "Console.App.Test").WithTag("language", "L1"));
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L2", groupIdentity: "Console.App.Test").WithTag("language", "L2"));
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L3", groupIdentity: "Console.App.Test").WithTag("language", "L3"));

            INewCommandInput userInputs = new MockNewCommandInput("console").WithHelpOption();

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.True(matchResult.HasExactMatches);
            Assert.Equal(1, matchResult.ExactMatchedTemplateGroups.Count);
            Assert.Equal(3, matchResult.ExactMatchedTemplates.Count);
            Assert.True(matchResult.HasUnambiguousTemplateGroup);
            Assert.Equal(3, matchResult.UnambiguousTemplateGroup.Count);
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

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.True(matchResult.HasExactMatches);
            Assert.Equal(2, matchResult.ExactMatchedTemplateGroups.Count);
            Assert.Equal(5, matchResult.ExactMatchedTemplates.Count);
            Assert.False(matchResult.HasUnambiguousTemplateGroup);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_DefaultLanguageDisambiguates))]
        public void TestGetTemplateResolutionResult_DefaultLanguageDisambiguates()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L1", groupIdentity: "Console.App.Test").WithTag("language", "L1"));
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L2", groupIdentity: "Console.App.Test").WithTag("language", "L2"));

            INewCommandInput userInputs = new MockNewCommandInput("console").WithHelpOption();

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, "L1");
            Assert.True(matchResult.HasExactMatches);
            Assert.True(matchResult.HasUnambiguousTemplateGroup);
            Assert.True(matchResult.HasUnambiguousTemplateGroupForDefaultLanguage);
            Assert.Equal("console", matchResult.UnambiguousTemplatesForDefaultLanguage.Single().Info.ShortNameList.Single());
            Assert.Equal("Console.App.L1", matchResult.UnambiguousTemplatesForDefaultLanguage.Single().Info.Identity);
            Assert.Equal("L1", matchResult.UnambiguousTemplatesForDefaultLanguage.Single().Info.Tags["language"].Choices.Keys.FirstOrDefault());
            Assert.Equal(2, matchResult.UnambiguousTemplateGroup.Count);
            Assert.Equal(1, matchResult.UnambiguousTemplatesForDefaultLanguage.Count);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_InputLanguageIsPreferredOverDefault))]
        public void TestGetTemplateResolutionResult_InputLanguageIsPreferredOverDefault()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L1", groupIdentity: "Console.App.Test").WithTag("language", "L1"));
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L2", groupIdentity: "Console.App.Test").WithTag("language", "L2"));

            INewCommandInput userInputs = new MockNewCommandInput("console", "L2").WithHelpOption();

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, "L1");
            Assert.True(matchResult.HasExactMatches);
            Assert.True(matchResult.HasUnambiguousTemplateGroup);
            Assert.Equal("console", matchResult.UnambiguousTemplateGroup.Single().Info.ShortNameList.Single());
            Assert.Equal("Console.App.L2", matchResult.UnambiguousTemplateGroup.Single().Info.Identity);
            Assert.Equal("L2", matchResult.UnambiguousTemplateGroup.Single().Info.Tags["language"].Choices.Keys.FirstOrDefault());
            Assert.Equal(1, matchResult.UnambiguousTemplateGroup.Count);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_UnambiguousGroup_TemplatesAreNotSameLanguage))]
        public void TestGetTemplateResolutionResult_UnambiguousGroup_TemplatesAreNotSameLanguage()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L1", groupIdentity: "Console.App.Test").WithTag("language", "L1"));
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L2", groupIdentity: "Console.App.Test").WithTag("language", "L2"));
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L3", groupIdentity: "Console.App.Test").WithTag("language", "L3"));

            INewCommandInput userInputs = new MockNewCommandInput("console").WithHelpOption();

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.True(matchResult.HasExactMatches);
            Assert.Equal(1, matchResult.ExactMatchedTemplateGroups.Count);
            Assert.Equal(3, matchResult.ExactMatchedTemplates.Count);
            Assert.True(matchResult.HasUnambiguousTemplateGroup);
            Assert.Equal(3, matchResult.UnambiguousTemplateGroup.Count);
            Assert.False(matchResult.AllTemplatesInUnambiguousTemplateGroupAreSameLanguage);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_UnambiguousGroup_TemplatesAreSameLanguage))]
        public void TestGetTemplateResolutionResult_UnambiguousGroup_TemplatesAreSameLanguage()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test").WithTag("language", "L1"));
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T2", groupIdentity: "Console.App.Test").WithTag("language", "L1"));
            templatesToSearch.Add(new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T3", groupIdentity: "Console.App.Test").WithTag("language", "L1"));

            INewCommandInput userInputs = new MockNewCommandInput("console").WithHelpOption();

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.True(matchResult.HasExactMatches);
            Assert.Equal(1, matchResult.ExactMatchedTemplateGroups.Count);
            Assert.Equal(3, matchResult.ExactMatchedTemplates.Count);
            Assert.True(matchResult.HasUnambiguousTemplateGroup);
            Assert.Equal(3, matchResult.UnambiguousTemplateGroup.Count);
            Assert.True(matchResult.AllTemplatesInUnambiguousTemplateGroupAreSameLanguage);
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

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.False(matchResult.HasExactMatches);
            Assert.Equal(0, matchResult.ExactMatchedTemplateGroups.Count);
            Assert.Equal(0, matchResult.ExactMatchedTemplates.Count);
            Assert.Equal(1, matchResult.PartiallyMatchedTemplates.Count);
            Assert.Equal(1, matchResult.PartiallyMatchedTemplateGroups.Count);
            Assert.True(matchResult.HasLanguageMismatch);
            Assert.False(matchResult.HasTypeMismatch);
            Assert.False(matchResult.HasBaselineMismatch);
            Assert.False(matchResult.HasUnambiguousTemplateGroup);
            Assert.Equal(0, matchResult.UnambiguousTemplateGroup.Count);
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

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.False(matchResult.HasExactMatches);
            Assert.Equal(0, matchResult.ExactMatchedTemplateGroups.Count);
            Assert.Equal(0, matchResult.ExactMatchedTemplates.Count);
            Assert.Equal(1, matchResult.PartiallyMatchedTemplates.Count);
            Assert.Equal(1, matchResult.PartiallyMatchedTemplateGroups.Count);
            Assert.False(matchResult.HasLanguageMismatch);
            Assert.True (matchResult.HasTypeMismatch);
            Assert.False(matchResult.HasBaselineMismatch);
            Assert.False(matchResult.HasUnambiguousTemplateGroup);
            Assert.Equal(0, matchResult.UnambiguousTemplateGroup.Count);
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

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.False(matchResult.HasExactMatches);
            Assert.Equal(0, matchResult.ExactMatchedTemplateGroups.Count);
            Assert.Equal(0, matchResult.ExactMatchedTemplates.Count);
            Assert.Equal(1, matchResult.PartiallyMatchedTemplates.Count);
            Assert.Equal(1, matchResult.PartiallyMatchedTemplateGroups.Count);
            Assert.False(matchResult.HasLanguageMismatch);
            Assert.False(matchResult.HasTypeMismatch);
            Assert.True(matchResult.HasBaselineMismatch);
            Assert.False(matchResult.HasUnambiguousTemplateGroup);
            Assert.Equal(0, matchResult.UnambiguousTemplateGroup.Count);
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

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.False(matchResult.HasExactMatches);
            Assert.Equal(0, matchResult.ExactMatchedTemplateGroups.Count);
            Assert.Equal(0, matchResult.ExactMatchedTemplates.Count);
            Assert.Equal(1, matchResult.PartiallyMatchedTemplates.Count);
            Assert.Equal(1, matchResult.PartiallyMatchedTemplateGroups.Count);
            Assert.True(matchResult.HasLanguageMismatch);
            Assert.True(matchResult.HasTypeMismatch);
            Assert.True(matchResult.HasBaselineMismatch);
            Assert.False(matchResult.HasUnambiguousTemplateGroup);
            Assert.Equal(0, matchResult.UnambiguousTemplateGroup.Count);
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

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.False(matchResult.HasExactMatches);
            Assert.Equal(0, matchResult.ExactMatchedTemplateGroups.Count);
            Assert.Equal(0, matchResult.ExactMatchedTemplates.Count);
            Assert.Equal(2, matchResult.PartiallyMatchedTemplates.Count);
            Assert.Equal(1, matchResult.PartiallyMatchedTemplateGroups.Count);
            Assert.False(matchResult.HasLanguageMismatch);
            Assert.True(matchResult.HasTypeMismatch);
            Assert.False(matchResult.HasBaselineMismatch);
            Assert.False(matchResult.HasUnambiguousTemplateGroup);
            Assert.Equal(0, matchResult.UnambiguousTemplateGroup.Count);
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

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.False(matchResult.HasExactMatches);
            Assert.False(matchResult.HasPartialMatches);
            Assert.Equal(0, matchResult.ExactMatchedTemplateGroups.Count);
            Assert.Equal(0, matchResult.ExactMatchedTemplates.Count);
            Assert.Equal(0, matchResult.PartiallyMatchedTemplates.Count);
            Assert.Equal(0, matchResult.PartiallyMatchedTemplateGroups.Count);
            Assert.False(matchResult.HasLanguageMismatch);
            Assert.False(matchResult.HasTypeMismatch);
            Assert.False(matchResult.HasBaselineMismatch);
            Assert.False(matchResult.HasUnambiguousTemplateGroup);
            Assert.Equal(0, matchResult.UnambiguousTemplateGroup.Count);
        }


        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_OtherParameterMatch_Text))]
        public void TestGetTemplateResolutionResult_OtherParameterMatch_Text()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();

            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test1")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithParameters("langVersion")
                    .WithBaselineInfo("app", "standard"));

            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T2", groupIdentity: "Console.App.Test2")
                    .WithTag("language", "L2")
                    .WithTag("type", "project")
                    .WithParameters("test")
                    .WithBaselineInfo("app", "standard"));

            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T3", groupIdentity: "Console.App.Test3")
                    .WithTag("language", "L3")
                    .WithTag("type", "project")
                    .WithBaselineInfo("app", "standard"));

            INewCommandInput userInputs = new MockNewCommandInput("c").WithHelpOption().WithTemplateOption("langVersion");

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.True(matchResult.HasExactMatches);
            Assert.Equal(1, matchResult.ExactMatchedTemplateGroups.Count);
            Assert.Equal(1, matchResult.ExactMatchedTemplates.Count);
            Assert.False(matchResult.HasLanguageMismatch);
            Assert.False(matchResult.HasTypeMismatch);
            Assert.False(matchResult.HasBaselineMismatch);
            Assert.True(matchResult.HasUnambiguousTemplateGroup);
            Assert.Equal(1, matchResult.UnambiguousTemplateGroup.Count);
            HelpForTemplateResolution.GetParametersInvalidForTemplatesInList(matchResult.ExactMatchedTemplates, out IReadOnlyList<string> invalidForAllTemplates, out IReadOnlyList<string> invalidForSomeTemplates);
            Assert.Equal(0, invalidForSomeTemplates.Count);
            Assert.Equal(0, invalidForAllTemplates.Count);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_OtherParameterMatch_Choice))]
        public void TestGetTemplateResolutionResult_OtherParameterMatch_Choice()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();

            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test1")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithTag("framework", "netcoreapp1.0", "netcoreapp1.1")
                    .WithBaselineInfo("app", "standard"));

            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T2", groupIdentity: "Console.App.Test2")
                    .WithTag("language", "L2")
                    .WithTag("type", "project")
                    .WithParameters("test")
                    .WithBaselineInfo("app", "standard"));

            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T3", groupIdentity: "Console.App.Test3")
                    .WithTag("language", "L3")
                    .WithTag("type", "project")
                    .WithBaselineInfo("app", "standard"));

            INewCommandInput userInputs = new MockNewCommandInput("c").WithHelpOption().WithTemplateOption("framework");

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.True(matchResult.HasExactMatches);
            Assert.Equal(3, matchResult.ExactMatchedTemplateGroups.Count);
            Assert.Equal(3, matchResult.ExactMatchedTemplates.Count);
            Assert.False(matchResult.HasLanguageMismatch);
            Assert.False(matchResult.HasTypeMismatch);
            Assert.False(matchResult.HasBaselineMismatch);
            Assert.False(matchResult.HasUnambiguousTemplateGroup);
            HelpForTemplateResolution.GetParametersInvalidForTemplatesInList(matchResult.ExactMatchedTemplates, out IReadOnlyList<string> invalidForAllTemplates, out IReadOnlyList<string> invalidForSomeTemplates);
            Assert.Equal(1, invalidForSomeTemplates.Count);
            Assert.Equal(0, invalidForAllTemplates.Count);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_OtherParameterDoesNotExist))]
        public void TestGetTemplateResolutionResult_OtherParameterDoesNotExist()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();

            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test1")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithTag("framework", "netcoreapp1.0", "netcoreapp1.1")
                    .WithBaselineInfo("app", "standard"));

            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T2", groupIdentity: "Console.App.Test2")
                    .WithTag("language", "L2")
                    .WithTag("type", "project")
                    .WithParameters("test")
                    .WithBaselineInfo("app", "standard"));

            templatesToSearch.Add(
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T3", groupIdentity: "Console.App.Test3")
                    .WithTag("language", "L3")
                    .WithTag("type", "project")
                    .WithBaselineInfo("app", "standard"));

            INewCommandInput userInputs = new MockNewCommandInput("c").WithHelpOption().WithTemplateOption("do-not-exist");

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.True(matchResult.HasExactMatches);
            Assert.Equal(3, matchResult.ExactMatchedTemplateGroups.Count);
            Assert.Equal(3, matchResult.ExactMatchedTemplates.Count);
            Assert.False(matchResult.HasLanguageMismatch);
            Assert.False(matchResult.HasTypeMismatch);
            Assert.False(matchResult.HasBaselineMismatch);
            Assert.False(matchResult.HasUnambiguousTemplateGroup);
            HelpForTemplateResolution.GetParametersInvalidForTemplatesInList(matchResult.ExactMatchedTemplates, out IReadOnlyList<string> invalidForAllTemplates, out IReadOnlyList<string> invalidForSomeTemplates);
            Assert.Equal(0, invalidForSomeTemplates.Count);
            Assert.Equal(1, invalidForAllTemplates.Count);
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

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.False(matchResult.HasExactMatches);
            Assert.False(matchResult.HasPartialMatches);
            Assert.Equal(0, matchResult.ExactMatchedTemplateGroups.Count);
            Assert.Equal(0, matchResult.ExactMatchedTemplates.Count);
            Assert.False(matchResult.HasLanguageMismatch);
            Assert.False(matchResult.HasTypeMismatch);
            Assert.False(matchResult.HasBaselineMismatch);
        }
    }
}
