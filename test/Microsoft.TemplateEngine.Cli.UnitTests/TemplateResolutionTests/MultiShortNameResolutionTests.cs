// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Cli.UnitTests.CliMocks;
using Microsoft.TemplateEngine.Mocks;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests.TemplateResolutionTests
{
    public class MultiShortNameResolutionTests
    {
        private static IReadOnlyList<ITemplateInfo>? _multiShortNameGroupTemplateInfo;

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
                            .WithChoiceParameter("foo", "A", "W")
                            .WithParameters("HighC"));

                    templateList.Add(
                        new MockTemplateInfo(new string[] { "ccc", "ddd", "eee" }, name: "Low precedence C# in group", precedence: 100, identity: "MultiName.Test.Low.CSharp", groupIdentity: "MultiName.Test")
                            .WithTag("language", "C#")
                            .WithChoiceParameter("foo", "A", "X")
                            .WithParameters("LowC"));

                    templateList.Add(
                       new MockTemplateInfo(new string[] { "fff" }, name: "Only F# in group", precedence: 100, identity: "Multiname.Test.Only.FSharp", groupIdentity: "MultiName.Test")
                           .WithTag("language", "F#")
                           .WithChoiceParameter("foo", "A", "Y")
                           .WithParameters("OnlyF"));

                    templateList.Add(
                        new MockTemplateInfo(new string[] { "other" }, name: "Unrelated template", precedence: 9999, identity: "Unrelated.Template.CSharp", groupIdentity: "Unrelated.Template")
                            .WithTag("language", "C#")
                            .WithChoiceParameter("foo", "A", "Z"));

                    _multiShortNameGroupTemplateInfo = templateList;
                }

                return _multiShortNameGroupTemplateInfo;
            }
        }

        private static readonly IReadOnlyList<string> _shortNamesForGroup = new [] { "aaa", "bbb", "ccc", "ddd", "eee", "fff" };

        [Fact(DisplayName = nameof(AllTemplatesInGroupUseAllShortNamesForResolution))]
        public async Task AllTemplatesInGroupUseAllShortNamesForResolution()
        {
            string defaultLanguage = "C#";
            IReadOnlyList<string> cSharpShortNames = new[] { "aaa", "bbb", "ccc", "ddd", "eee" };
            IReadOnlyList<string> fSharpShortNames = new[] { "fff" };

            foreach (string testShortName in _shortNamesForGroup)
            {
                INewCommandInput userInputs = new MockNewCommandInput(testShortName);
                InstantiateTemplateResolver resolver = new InstantiateTemplateResolver(MultiShortNameGroupTemplateInfo, new MockHostSpecificDataLoader());
                TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(userInputs, defaultLanguage, default).ConfigureAwait(false);

                Assert.Equal(TemplateResolutionResult.TemplateGroupStatus.SingleMatch, matchResult.GroupResolutionStatus);
                Assert.Equal(3, matchResult.UnambiguousTemplateGroup?.Templates.Count);

                Assert.NotNull(matchResult.UnambiguousTemplateGroupMatchInfo);
                Assert.Equal(2, matchResult.UnambiguousTemplateGroupMatchInfo!.GroupMatchInfos.Count());
                Assert.True(matchResult.UnambiguousTemplateGroupMatchInfo!.GroupMatchInfos.All(mi => mi.Kind == MatchKind.Exact));
                Assert.Contains(MatchInfo.BuiltIn.ShortName, matchResult.UnambiguousTemplateGroupMatchInfo!.GroupMatchInfos.Select(mi => mi.Name));
                Assert.Contains(MatchInfo.BuiltIn.Language, matchResult.UnambiguousTemplateGroupMatchInfo!.GroupMatchInfos.Select(mi => mi.Name)); 
                Assert.Equal(_shortNamesForGroup, matchResult.UnambiguousTemplateGroup!.ShortNames);
            }
        }

        [Fact(DisplayName = nameof(HighestPrecedenceWinsWithMultipleShortNames))]
        public async Task HighestPrecedenceWinsWithMultipleShortNames()
        {
            foreach (string testShortName in _shortNamesForGroup)
            {
                INewCommandInput userInputs = new MockNewCommandInput(testShortName);
                InstantiateTemplateResolver resolver = new InstantiateTemplateResolver(MultiShortNameGroupTemplateInfo, new MockHostSpecificDataLoader());
                TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(userInputs, "C#", default).ConfigureAwait(false);

                Assert.Equal(TemplateResolutionResult.Status.SingleMatch, matchResult.ResolutionStatus);
                Assert.Equal("MultiName.Test.High.CSharp", matchResult.TemplateToInvoke?.Template.Identity);
                Assert.Equal(_shortNamesForGroup, matchResult.UnambiguousTemplateGroup?.ShortNames);
            }
        }

        [Fact(DisplayName = nameof(ExplicitLanguageChoiceIsHonoredWithMultipleShortNames))]
        public async Task ExplicitLanguageChoiceIsHonoredWithMultipleShortNames()
        {
            foreach (string testShortName in _shortNamesForGroup)
            {
                INewCommandInput userInputs = new MockNewCommandInput(testShortName, "F#");
                InstantiateTemplateResolver resolver = new InstantiateTemplateResolver(MultiShortNameGroupTemplateInfo, new MockHostSpecificDataLoader());
                TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(userInputs, "C#", default).ConfigureAwait(false);

                Assert.Equal(TemplateResolutionResult.Status.SingleMatch, matchResult.ResolutionStatus);
                Assert.Equal("Multiname.Test.Only.FSharp", matchResult.TemplateToInvoke?.Template.Identity);
                Assert.Equal(_shortNamesForGroup, matchResult.UnambiguousTemplateGroup?.ShortNames);
            }
        }

        [Theory(DisplayName = nameof(ChoiceValueDisambiguatesMatchesWithMultipleShortNames))]
        [InlineData("aaa", "W", "MultiName.Test.High.CSharp")] // uses a short name from the expected invokable template
        [InlineData("fff", "W", "MultiName.Test.High.CSharp")] // uses a short name from a different template in the group
        [InlineData("ccc", "X", "MultiName.Test.Low.CSharp")] // uses a short name from the expected invokable template
        [InlineData("fff", "X", "MultiName.Test.Low.CSharp")] // uses a short name from a different template in the group
        [InlineData("fff", "Y", "Multiname.Test.Only.FSharp")] // uses a short name from the expected invokable template
        [InlineData("eee", "Y", "Multiname.Test.Only.FSharp")] // uses a short name from a different template in the group
        public async Task ChoiceValueDisambiguatesMatchesWithMultipleShortNames(string name, string fooChoice, string expectedIdentity)
        {
            INewCommandInput commandInput = new MockNewCommandInput(name).WithTemplateOption("foo", fooChoice);
            InstantiateTemplateResolver resolver = new InstantiateTemplateResolver(MultiShortNameGroupTemplateInfo, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(commandInput, "C#", default).ConfigureAwait(false);

            Assert.Equal(TemplateResolutionResult.Status.SingleMatch, matchResult.ResolutionStatus);
            Assert.Equal(expectedIdentity, matchResult.TemplateToInvoke?.Template.Identity);
            Assert.Equal(_shortNamesForGroup, matchResult.UnambiguousTemplateGroup?.ShortNames);
        }

        [Theory(DisplayName = nameof(ParameterExistenceDisambiguatesMatchesWithMultipleShortNames))]
        [InlineData("aaa", "HighC", "someValue", "MultiName.Test.High.CSharp")] // uses a short name from the expected invokable template
        [InlineData("fff", "HighC", "someValue", "MultiName.Test.High.CSharp")] // uses a short name from a different template in the group
        [InlineData("ccc", "LowC", "someValue", "MultiName.Test.Low.CSharp")] // uses a short name from the expected invokable template
        [InlineData("fff", "LowC", "someValue", "MultiName.Test.Low.CSharp")] // uses a short name from a different template in the group
        [InlineData("fff", "OnlyF", "someValue", "Multiname.Test.Only.FSharp")] // uses a short name from the expected invokable template
        [InlineData("eee", "OnlyF", "someValue", "Multiname.Test.Only.FSharp")] // uses a short name from a different template in the group
        public async Task ParameterExistenceDisambiguatesMatchesWithMultipleShortNames(string name, string paramName, string paramValue, string expectedIdentity)
        {
            INewCommandInput commandInput = new MockNewCommandInput(name).WithTemplateOption(paramName, paramValue);
            InstantiateTemplateResolver resolver = new InstantiateTemplateResolver(MultiShortNameGroupTemplateInfo, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(commandInput, "C#", default).ConfigureAwait(false);

            Assert.Equal(TemplateResolutionResult.Status.SingleMatch, matchResult.ResolutionStatus);
            Assert.Equal(expectedIdentity, matchResult.TemplateToInvoke?.Template.Identity);
            Assert.Equal(_shortNamesForGroup, matchResult.UnambiguousTemplateGroup?.ShortNames);
        }
    }
}
