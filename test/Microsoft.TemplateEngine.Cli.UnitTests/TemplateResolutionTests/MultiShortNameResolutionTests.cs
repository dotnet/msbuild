// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Cli.UnitTests.CliMocks;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.Utils;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests.TemplateResolutionTests
{
    public class MultiShortNameResolutionTests
    {
        private static IReadOnlyList<ITemplateInfo> _multiShortNameGroupTemplateInfo;

        private static IReadOnlyList<ITemplateInfo> MultiShortNameGroupTemplateInfo
        {
            get
            {
                if (_multiShortNameGroupTemplateInfo == null)
                {
                    List<ITemplateInfo> templateList = new List<ITemplateInfo>();

                    templateList.Add(
                        new MockTemplateInfo(new string[] { "aaa", "bbb" }, name: "High precedence C# in group", precedence: 2000, identity: "MultiName.Test.High.CSharp", groupIdentity: "MultiName.Test")
                            .WithTag("language", "C#")
                            .WithTag("foo", "A", "W")
                            .WithParameters("HighC"));

                    templateList.Add(
                        new MockTemplateInfo(new string[] { "ccc", "ddd", "eee" }, name: "Low precedence C# in group", precedence: 100, identity: "MultiName.Test.Low.CSharp", groupIdentity: "MultiName.Test")
                            .WithTag("language", "C#")
                            .WithTag("foo", "A", "X")
                            .WithParameters("LowC"));

                    templateList.Add(
                       new MockTemplateInfo(new string[] { "fff" }, name: "Only F# in group", precedence: 100, identity: "Multiname.Test.Only.FSharp", groupIdentity: "MultiName.Test")
                           .WithTag("language", "F#")
                           .WithTag("foo", "A", "Y")
                           .WithParameters("OnlyF"));

                    templateList.Add(
                        new MockTemplateInfo(new string[] { "other" }, name: "Unrelated template", precedence: 9999, identity: "Unrelated.Template.CSharp", groupIdentity: "Unrelated.Template")
                            .WithTag("language", "C#")
                            .WithTag("foo", "A", "Z"));

                    _multiShortNameGroupTemplateInfo = templateList;
                }

                return _multiShortNameGroupTemplateInfo;
            }
        }

        [Fact(DisplayName = nameof(AllTemplatesInGroupUseAllShortNamesForResolution))]
        public void AllTemplatesInGroupUseAllShortNamesForResolution()
        {
            IReadOnlyList<string> shortNamesForGroup = new List<string>()
            {
                "aaa", "bbb", "ccc", "ddd", "eee", "fff"
            };
            string defaultLanguage = "C#";

            foreach (string testShortName in shortNamesForGroup)
            {
                INewCommandInput userInputs = new MockNewCommandInput(testShortName);

                TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(MultiShortNameGroupTemplateInfo, new MockHostSpecificDataLoader(), userInputs, defaultLanguage);
                Assert.Equal(TemplateResolutionResult.UnambiguousTemplateGroupStatus.SingleMatch, matchResult.GroupResolutionStatus);
                Assert.Equal(3, matchResult.UnambiguousTemplateGroup.Templates.Count);
                Assert.True(matchResult.UnambiguousTemplateGroup.Templates.All(t => WellKnownSearchFilters.MatchesAllCriteria(t)));

                foreach (ITemplateMatchInfo templateMatchInfo in matchResult.UnambiguousTemplateGroup.Templates)
                {
                    Assert.Equal("MultiName.Test", templateMatchInfo.Info.GroupIdentity);
                    if (templateMatchInfo.Info.GetLanguage().Equals(defaultLanguage, StringComparison.OrdinalIgnoreCase))
                    {
                        //default language match is part of MatchDisposition collection
                        Assert.Equal(2, templateMatchInfo.MatchDisposition.Count);
                    }
                    else
                    {
                        Assert.Equal(1, templateMatchInfo.MatchDisposition.Count);
                    }
                    Assert.True(templateMatchInfo.MatchDisposition[0].Name == MatchInfo.BuiltIn.ShortName && templateMatchInfo.MatchDisposition[0].Kind == MatchKind.Exact);
                }
            }
        }

        [Fact(DisplayName = nameof(HighestPrecedenceWinsWithMultipleShortNames))]
        public void HighestPrecedenceWinsWithMultipleShortNames()
        {
            IReadOnlyList<string> shortNamesForGroup = new List<string>()
            {
                "aaa", "bbb", "ccc", "ddd", "eee", "fff"
            };

            foreach (string testShortName in shortNamesForGroup)
            {
                INewCommandInput userInputs = new MockNewCommandInput(testShortName);

                TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(MultiShortNameGroupTemplateInfo, new MockHostSpecificDataLoader(), userInputs, "C#");
                Assert.Equal(TemplateResolutionResult.Status.SingleMatch, matchResult.ResolutionStatus);
                Assert.Equal("MultiName.Test.High.CSharp", matchResult.TemplateToInvoke.Info.Identity);
            }
        }

        [Fact(DisplayName = nameof(ExplicitLanguageChoiceIsHonoredWithMultipleShortNames))]
        public void ExplicitLanguageChoiceIsHonoredWithMultipleShortNames()
        {
            IReadOnlyList<string> shortNamesForGroup = new List<string>()
            {
                "aaa", "bbb", "ccc", "ddd", "eee", "fff"
            };

            foreach (string testShortName in shortNamesForGroup)
            {
                INewCommandInput userInputs = new MockNewCommandInput(testShortName, "F#");

                TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(MultiShortNameGroupTemplateInfo, new MockHostSpecificDataLoader(), userInputs, "C#");
                Assert.Equal(TemplateResolutionResult.Status.SingleMatch, matchResult.ResolutionStatus);
                Assert.Equal("Multiname.Test.Only.FSharp", matchResult.TemplateToInvoke.Info.Identity);
            }
        }

        [Theory(DisplayName = nameof(ChoiceValueDisambiguatesMatchesWithMultipleShortNames))]
        [InlineData("aaa", "W", "MultiName.Test.High.CSharp")] // uses a short name from the expected invokable template
        [InlineData("fff", "W", "MultiName.Test.High.CSharp")] // uses a short name from a different template in the group
        [InlineData("ccc", "X", "MultiName.Test.Low.CSharp")] // uses a short name from the expected invokable template
        [InlineData("fff", "X", "MultiName.Test.Low.CSharp")] // uses a short name from a different template in the group
        [InlineData("fff", "Y", "Multiname.Test.Only.FSharp")] // uses a short name from the expected invokable template
        [InlineData("eee", "Y", "Multiname.Test.Only.FSharp")] // uses a short name from a different template in the group
        public void ChoiceValueDisambiguatesMatchesWithMultipleShortNames(string name, string fooChoice, string expectedIdentity)
        {
            INewCommandInput commandInput = new MockNewCommandInput(name).WithTemplateOption("foo", fooChoice);

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(MultiShortNameGroupTemplateInfo, new MockHostSpecificDataLoader(), commandInput, "C#");
            Assert.Equal(TemplateResolutionResult.Status.SingleMatch, matchResult.ResolutionStatus);
            Assert.Equal(expectedIdentity, matchResult.TemplateToInvoke.Info.Identity);
        }

        [Theory(DisplayName = nameof(ParameterExistenceDisambiguatesMatchesWithMultipleShortNames))]
        [InlineData("aaa", "HighC", "someValue", "MultiName.Test.High.CSharp")] // uses a short name from the expected invokable template
        [InlineData("fff", "HighC", "someValue", "MultiName.Test.High.CSharp")] // uses a short name from a different template in the group
        [InlineData("ccc", "LowC", "someValue", "MultiName.Test.Low.CSharp")] // uses a short name from the expected invokable template
        [InlineData("fff", "LowC", "someValue", "MultiName.Test.Low.CSharp")] // uses a short name from a different template in the group
        [InlineData("fff", "OnlyF", "someValue", "Multiname.Test.Only.FSharp")] // uses a short name from the expected invokable template
        [InlineData("eee", "OnlyF", "someValue", "Multiname.Test.Only.FSharp")] // uses a short name from a different template in the group
        public void ParameterExistenceDisambiguatesMatchesWithMultipleShortNames(string name, string paramName, string paramValue, string expectedIdentity)
        {
            INewCommandInput commandInput = new MockNewCommandInput(name).WithTemplateOption(paramName, paramValue);

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(MultiShortNameGroupTemplateInfo, new MockHostSpecificDataLoader(), commandInput, "C#");
            Assert.Equal(TemplateResolutionResult.Status.SingleMatch, matchResult.ResolutionStatus);
            Assert.Equal(expectedIdentity, matchResult.TemplateToInvoke.Info.Identity);
        }
    }
}
