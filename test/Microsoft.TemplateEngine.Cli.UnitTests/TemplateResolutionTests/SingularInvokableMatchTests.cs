using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Cli.UnitTests.CliMocks;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Mocks;
using Xunit;
using static Microsoft.TemplateEngine.Cli.TemplateResolution.TemplateResolutionResult;

namespace Microsoft.TemplateEngine.Cli.UnitTests.TemplateResolutionTests
{
    public class SingularInvokableMatchTests
    {
        [Fact(DisplayName = nameof(MultipleTemplatesInGroupHavingSingleStartsWithOnSameParamIsAmbiguous))]
        public void MultipleTemplatesInGroupHavingSingleStartsWithOnSameParamIsAmbiguous()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("foo", name: "Foo template", identity: "foo.test_1", groupIdentity: "foo.test.template", precedence: 100)
                                    .WithTag("MyChoice", "value_1"));
            templatesToSearch.Add(new MockTemplateInfo("foo", name: "Foo template", identity: "foo.test_2", groupIdentity: "foo.test.template", precedence: 200)
                                    .WithTag("MyChoice", "value_2"));

            INewCommandInput userInputs = new MockNewCommandInput("foo").WithTemplateOption("MyChoice", "value_");

            IHostSpecificDataLoader hostSpecificDataLoader = new MockHostSpecificDataLoader();
            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(templatesToSearch, hostSpecificDataLoader, userInputs, null);
            // make sure there's an unambiguous group, otherwise the singular match check is meaningless
            Assert.True(matchResult.TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousGroup));
            Assert.Equal(2, unambiguousGroup.Count);
            Assert.Equal(2, matchResult.GetBestTemplateMatchList().Count);

            Assert.False(matchResult.TryGetSingularInvokableMatch(out ITemplateMatchInfo singularInvokableMatch, out Status resultStatus));
            Assert.Equal(Status.AmbiguousChoice, resultStatus);
            Assert.Null(singularInvokableMatch);
        }

        [Fact(DisplayName = nameof(MultipleTemplatesInGroupParamPartiaMatch_TheOneHavingSingleStartsWithIsTheSingularInvokableMatch))]
        public void MultipleTemplatesInGroupParamPartiaMatch_TheOneHavingSingleStartsWithIsTheSingularInvokableMatch()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("foo", name: "Foo template", identity: "foo.test_1", groupIdentity: "foo.test.template", precedence: 100)
                                    .WithTag("MyChoice", "value_1"));
            templatesToSearch.Add(new MockTemplateInfo("foo", name: "Foo template", identity: "foo.test_2", groupIdentity: "foo.test.template", precedence: 200)
                                    .WithTag("MyChoice", "value_2", "value_3"));

            INewCommandInput userInputs = new MockNewCommandInput("foo").WithTemplateOption("MyChoice", "value_");

            IHostSpecificDataLoader hostSpecificDataLoader = new MockHostSpecificDataLoader();
            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(templatesToSearch, hostSpecificDataLoader, userInputs, null);
            // make sure there's an unambiguous group, otherwise the singular match check is meaningless
            Assert.True(matchResult.TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousGroup));
            Assert.Equal(2, unambiguousGroup.Count);
            Assert.Equal(2, matchResult.GetBestTemplateMatchList().Count);

            Assert.True(matchResult.TryGetSingularInvokableMatch(out ITemplateMatchInfo singularInvokableMatch, out Status resultStatus));
            Assert.Equal(Status.SingleMatch, resultStatus);
            Assert.Equal("foo.test_1", singularInvokableMatch.Info.Identity);
        }

        [Fact(DisplayName = nameof(MultipleTemplatesInGroupHavingAmbiguousParamMatchOnSameParamIsAmbiguous))]
        public void MultipleTemplatesInGroupHavingAmbiguousParamMatchOnSameParamIsAmbiguous()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("foo", name: "Foo template", identity: "foo.test_1", groupIdentity: "foo.test.template", precedence: 100)
                        .WithTag("MyChoice", "value_1", "value_2"));
            templatesToSearch.Add(new MockTemplateInfo("foo", name: "Foo template", identity: "foo.test_2", groupIdentity: "foo.test.template", precedence: 200)
                                    .WithTag("MyChoice", "value_3", "value_4"));

            INewCommandInput userInputs = new MockNewCommandInput("foo").WithTemplateOption("MyChoice", "value_");

            IHostSpecificDataLoader hostSpecificDataLoader = new MockHostSpecificDataLoader();
            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(templatesToSearch, hostSpecificDataLoader, userInputs, null);
            // make sure there's an unambiguous group, otherwise the singular match check is meaningless
            Assert.True(matchResult.TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousGroup));
            Assert.Equal(2, unambiguousGroup.Count);
            Assert.Equal(2, matchResult.GetBestTemplateMatchList().Count);

            Assert.False(matchResult.TryGetSingularInvokableMatch(out ITemplateMatchInfo singularInvokableMatch, out Status resultStatus));
            Assert.Equal(Status.NoMatch, resultStatus);
            Assert.Null(singularInvokableMatch);
        }

        [Fact(DisplayName = nameof(MultipleTemplatesInGroupHavingSingularStartMatchesOnDifferentParams_HighPrecedenceIsChosen))]
        public void MultipleTemplatesInGroupHavingSingularStartMatchesOnDifferentParams_HighPrecedenceIsChosen()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("foo", name: "Foo template", identity: "foo.test_1", groupIdentity: "foo.test.template", precedence: 100)
                                    .WithTag("MyChoice", "value_1", "other_value")
                                    .WithTag("OtherChoice", "foo_"));
            templatesToSearch.Add(new MockTemplateInfo("foo", name: "Foo template", identity: "foo.test_2", groupIdentity: "foo.test.template", precedence: 200)
                                    .WithTag("MyChoice", "value_")
                                    .WithTag("OtherChoice", "foo_", "bar_1"));

            INewCommandInput userInputs = new MockNewCommandInput("foo").WithTemplateOption("MyChoice", "value_").WithTemplateOption("OtherChoice", "foo_");

            IHostSpecificDataLoader hostSpecificDataLoader = new MockHostSpecificDataLoader();
            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(templatesToSearch, hostSpecificDataLoader, userInputs, null);
            // make sure there's an unambiguous group, otherwise the singular match check is meaningless
            Assert.True(matchResult.TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousGroup));
            Assert.Equal(2, unambiguousGroup.Count);
            Assert.Equal(2, matchResult.GetBestTemplateMatchList().Count);

            Assert.True(matchResult.TryGetSingularInvokableMatch(out ITemplateMatchInfo singularInvokableMatch, out Status resultStatus));
            Assert.Equal(Status.SingleMatch, resultStatus);
            Assert.Equal("foo.test_2", singularInvokableMatch.Info.Identity);
        }

        [Fact(DisplayName = nameof(GivenOneInvokableTemplateWithNonDefaultLanguage_ItIsChosen))]
        public void GivenOneInvokableTemplateWithNonDefaultLanguage_ItIsChosen()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("foo", name: "Foo template", identity: "foo.test_1", groupIdentity: "foo.test.template", precedence: 100)
                                    .WithTag("language", "F#"));

            INewCommandInput userInputs = new MockNewCommandInput("foo");

            IHostSpecificDataLoader hostSpecificDataLoader = new MockHostSpecificDataLoader();
            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(templatesToSearch, hostSpecificDataLoader, userInputs, null);
            // make sure there's an unambiguous group, otherwise the singular match check is meaningless
            Assert.True(matchResult.TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousGroup));
            Assert.Equal(1, unambiguousGroup.Count);
            Assert.Equal(1, matchResult.GetBestTemplateMatchList().Count);

            Assert.True(matchResult.TryGetSingularInvokableMatch(out ITemplateMatchInfo singularInvokableMatch, out Status resultStatus));
            Assert.Equal(Status.SingleMatch, resultStatus);
            Assert.Equal("foo.test_1", singularInvokableMatch.Info.Identity);
        }

        [Fact(DisplayName = nameof(GivenTwoInvokableTemplatesNonDefaultLanguage_HighPrecedenceIsChosen))]
        public void GivenTwoInvokableTemplatesNonDefaultLanguage_HighPrecedenceIsChosen()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("foo", name: "Foo template", identity: "foo.test_1.FSharp", groupIdentity: "foo.test.template", precedence: 100)
                        .WithTag("language", "F#"));
            templatesToSearch.Add(new MockTemplateInfo("foo", name: "Foo template", identity: "foo.test_1.VB", groupIdentity: "foo.test.template", precedence: 200)
                  .WithTag("language", "VB"));

            INewCommandInput userInputs = new MockNewCommandInput("foo");

            IHostSpecificDataLoader hostSpecificDataLoader = new MockHostSpecificDataLoader();
            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(templatesToSearch, hostSpecificDataLoader, userInputs, null);
            // make sure there's an unambiguous group, otherwise the singular match check is meaningless
            Assert.True(matchResult.TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousGroup));
            Assert.Equal(2, unambiguousGroup.Count);
            Assert.Equal(2, matchResult.GetBestTemplateMatchList().Count);

            Assert.True(matchResult.TryGetSingularInvokableMatch(out ITemplateMatchInfo singularInvokableMatch, out Status resultStatus));
            Assert.Equal(Status.SingleMatch, resultStatus);
            Assert.Equal("foo.test_1.VB", singularInvokableMatch.Info.Identity);
        }

        [Fact(DisplayName = nameof(GivenMultipleHighestPrecedenceTemplates_ResultIsAmbiguous))]
        public void GivenMultipleHighestPrecedenceTemplates_ResultIsAmbiguous()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new MockTemplateInfo("foo", name: "Foo template", identity: "foo.test_1.FSharp", groupIdentity: "foo.test.template", precedence: 100)
                 .WithTag("language", "F#"));
            templatesToSearch.Add(new MockTemplateInfo("foo", name: "Foo template", identity: "foo.test_1.VB", groupIdentity: "foo.test.template", precedence: 100)
                  .WithTag("language", "VB"));

            INewCommandInput userInputs = new MockNewCommandInput("foo");

            IHostSpecificDataLoader hostSpecificDataLoader = new MockHostSpecificDataLoader();
            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(templatesToSearch, hostSpecificDataLoader, userInputs, null);
            // make sure there's an unambiguous group, otherwise the singular match check is meaningless
            Assert.True(matchResult.TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousGroup));
            Assert.Equal(2, unambiguousGroup.Count);
            Assert.Equal(2, matchResult.GetBestTemplateMatchList().Count);

            Assert.False(matchResult.TryGetSingularInvokableMatch(out ITemplateMatchInfo singularInvokableMatch, out Status resultStatus));
            Assert.Null(singularInvokableMatch);
            Assert.Equal(Status.AmbiguousPrecedence, resultStatus);
        }
    }
}
