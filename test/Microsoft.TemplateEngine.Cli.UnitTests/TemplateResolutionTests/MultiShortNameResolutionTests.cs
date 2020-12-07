using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Cli.UnitTests.CliMocks;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests.TemplateResolutionTests
{
    public class MultiShortNameResolutionTests
    {
        [Fact(DisplayName = nameof(AllTemplatesInGroupUseAllShortNamesForResolution))]
        public void AllTemplatesInGroupUseAllShortNamesForResolution()
        {
            IReadOnlyList<string> shortNamesForGroup = new List<string>()
            {
                "aaa", "bbb", "ccc", "ddd", "eee", "fff"
            };

            foreach (string testShortName in shortNamesForGroup)
            {
                INewCommandInput userInputs = new MockNewCommandInput()
                {
                    TemplateName = testShortName
                };

                TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(MultiShortNameGroupTemplateInfo, new MockHostSpecificDataLoader(), userInputs, "C#");
                matchResult.TryGetCoreMatchedTemplatesWithDisposition(x => x.IsMatch, out IReadOnlyList<ITemplateMatchInfo> matchedTemplateList);
                Assert.Equal(3, matchedTemplateList.Count);

                foreach (ITemplateMatchInfo templateMatchInfo in matchedTemplateList)
                {
                    Assert.Equal("MultiName.Test", templateMatchInfo.Info.GroupIdentity);
                    Assert.Equal(1, templateMatchInfo.MatchDisposition.Count);
                    Assert.True(templateMatchInfo.MatchDisposition[0].Location == MatchLocation.ShortName && templateMatchInfo.MatchDisposition[0].Kind == MatchKind.Exact);
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
                INewCommandInput userInputs = new MockNewCommandInput()
                {
                    TemplateName = testShortName
                };

                TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(MultiShortNameGroupTemplateInfo, new MockHostSpecificDataLoader(), userInputs, "C#");
                Assert.True(matchResult.TryGetSingularInvokableMatch(out ITemplateMatchInfo invokableTemplate, out TemplateResolutionResult.Status resultStatus));
                Assert.Equal(TemplateResolutionResult.Status.SingleMatch, resultStatus);
                Assert.Equal("MultiName.Test.High.CSharp", invokableTemplate.Info.Identity);
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
                INewCommandInput userInputs = new MockNewCommandInput()
                {
                    TemplateName = testShortName,
                    Language = "F#"
                };

                TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(MultiShortNameGroupTemplateInfo, new MockHostSpecificDataLoader(), userInputs, "C#");
                Assert.True(matchResult.TryGetSingularInvokableMatch(out ITemplateMatchInfo invokableTemplate, out TemplateResolutionResult.Status resultStatus));
                Assert.Equal(TemplateResolutionResult.Status.SingleMatch, resultStatus);
                Assert.Equal("Multiname.Test.Only.FSharp", invokableTemplate.Info.Identity);
            }
        }

        [Theory(DisplayName = nameof(ChoiceValueDisambiguatesMatchesWithMultipleShortNames))]
        [InlineData("aaa", "W", "MultiName.Test.High.CSharp")]  // uses a short name from the expected invokable template
        [InlineData("fff", "W", "MultiName.Test.High.CSharp")]  // uses a short name from a different template in the group
        [InlineData("ccc", "X", "MultiName.Test.Low.CSharp")]   // uses a short name from the expected invokable template
        [InlineData("fff", "X", "MultiName.Test.Low.CSharp")]   // uses a short name from a different template in the group
        [InlineData("fff", "Y", "Multiname.Test.Only.FSharp")]  // uses a short name from the expected invokable template
        [InlineData("eee", "Y", "Multiname.Test.Only.FSharp")]  // uses a short name from a different template in the group
        public void ChoiceValueDisambiguatesMatchesWithMultipleShortNames(string name, string fooChoice, string expectedIdentity)
        {
            IReadOnlyDictionary<string, string> userParams = new Dictionary<string, string>()
            {
                {  "foo", fooChoice }
            };

            INewCommandInput commandInput = new MockNewCommandInput(userParams)
            {
                TemplateName = name
            };

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(MultiShortNameGroupTemplateInfo, new MockHostSpecificDataLoader(), commandInput, "C#");

            Assert.True(matchResult.TryGetSingularInvokableMatch(out ITemplateMatchInfo invokableTemplate, out TemplateResolutionResult.Status resultStatus));
            Assert.Equal(TemplateResolutionResult.Status.SingleMatch, resultStatus);
            Assert.Equal(expectedIdentity, invokableTemplate.Info.Identity);
        }

        [Theory(DisplayName = nameof(ParameterExistenceDisambiguatesMatchesWithMultipleShortNames))]
        [InlineData("aaa", "HighC", "someValue", "MultiName.Test.High.CSharp")] // uses a short name from the expected invokable template
        [InlineData("fff", "HighC", "someValue", "MultiName.Test.High.CSharp")] // uses a short name from a different template in the group
        [InlineData("ccc", "LowC", "someValue", "MultiName.Test.Low.CSharp")]   // uses a short name from the expected invokable template
        [InlineData("fff", "LowC", "someValue", "MultiName.Test.Low.CSharp")]   // uses a short name from a different template in the group
        [InlineData("fff", "OnlyF", "someValue", "Multiname.Test.Only.FSharp")] // uses a short name from the expected invokable template
        [InlineData("eee", "OnlyF", "someValue", "Multiname.Test.Only.FSharp")] // uses a short name from a different template in the group
        public void ParameterExistenceDisambiguatesMatchesWithMultipleShortNames(string name, string paramName, string paramValue, string expectedIdentity)
        {
            IReadOnlyDictionary<string, string> userParams = new Dictionary<string, string>()
            {
                { paramName, paramValue }
            };

            INewCommandInput commandInput = new MockNewCommandInput(userParams)
            {
                TemplateName = name
            };

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(MultiShortNameGroupTemplateInfo, new MockHostSpecificDataLoader(), commandInput, "C#");

            Assert.True(matchResult.TryGetSingularInvokableMatch(out ITemplateMatchInfo invokableTemplate, out TemplateResolutionResult.Status resultStatus));
            Assert.Equal(TemplateResolutionResult.Status.SingleMatch, resultStatus);
            Assert.Equal(expectedIdentity, invokableTemplate.Info.Identity);
        }

        private static IReadOnlyList<ITemplateInfo> MultiShortNameGroupTemplateInfo
        {
            get
            {
                if (_multiShortNameGroupTemplateInfo == null)
                {
                    List<ITemplateInfo> templateList = new List<ITemplateInfo>();

                    templateList.Add(new TemplateInfo()
                    {
                        ShortNameList = new string[] { "aaa", "bbb" },
                        Name = "High precedence C# in group",
                        Precedence = 2000,
                        Identity = "MultiName.Test.High.CSharp",
                        GroupIdentity = "MultiName.Test",
                        Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                        {
                            { "language", ResolutionTestHelper.CreateTestCacheTag("C#") },
                            { "foo", ResolutionTestHelper.CreateTestCacheTag(new string[] { "A", "W" }) }
                        },
                        CacheParameters = new Dictionary<string, ICacheParameter>(StringComparer.OrdinalIgnoreCase)
                        {
                            { "HighC", new CacheParameter("string", "high c", "high c description") }
                        }
                    });

                    templateList.Add(new TemplateInfo()
                    {
                        ShortNameList = new string[] { "ccc", "ddd", "eee" },
                        Name = "Low precedence C# in group",
                        Precedence = 100,
                        Identity = "MultiName.Test.Low.CSharp",
                        GroupIdentity = "MultiName.Test",
                        Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                        {
                            { "language", ResolutionTestHelper.CreateTestCacheTag("C#") },
                            { "foo", ResolutionTestHelper.CreateTestCacheTag(new string[] { "A", "X" }) }
                        },
                        CacheParameters = new Dictionary<string, ICacheParameter>(StringComparer.OrdinalIgnoreCase)
                        {
                            { "LowC", new CacheParameter("string", "low c", "low c description") }
                        }
                    });

                    templateList.Add(new TemplateInfo()
                    {
                        ShortNameList = new string[] { "fff" },
                        Name = "Only F# in group",
                        Precedence = 100,
                        Identity = "Multiname.Test.Only.FSharp",
                        GroupIdentity = "MultiName.Test",
                        Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                        {
                            { "language", ResolutionTestHelper.CreateTestCacheTag("F#") },
                            { "foo", ResolutionTestHelper.CreateTestCacheTag(new string[] { "A", "Y" }) }
                        },
                        CacheParameters = new Dictionary<string, ICacheParameter>(StringComparer.OrdinalIgnoreCase)
                        {
                            { "OnlyF", new CacheParameter("string", "only f", "only f description") }
                        }
                    });

                    templateList.Add(new TemplateInfo()
                    {
                        ShortNameList = new string[] { "other" },
                        Name = "Unrelated template",
                        Precedence = 9999,
                        Identity = "Unrelated.Template.CSharp",
                        GroupIdentity = "Unrelated.Template",
                        Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                        {
                            { "language", ResolutionTestHelper.CreateTestCacheTag("C#") },
                            { "foo", ResolutionTestHelper.CreateTestCacheTag(new string[] { "A", "Z" }) }
                        },
                        CacheParameters = new Dictionary<string, ICacheParameter>(StringComparer.OrdinalIgnoreCase)
                    });

                    _multiShortNameGroupTemplateInfo = templateList;
                }

                return _multiShortNameGroupTemplateInfo;
            }
        }
        private static IReadOnlyList<ITemplateInfo> _multiShortNameGroupTemplateInfo;
    }
}
