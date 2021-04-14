// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Cli.HelpAndUsage;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Mocks;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests.HelpTests
{
    public class HelpForTemplateResolutionTests
    {
        [Fact(DisplayName = nameof(GetParametersInvalidForTemplatesInListTest))]
        public void GetParametersInvalidForTemplatesInListTest()
        {
            List<ITemplateMatchInfo> matchInfo = new List<ITemplateMatchInfo>();

            // template one
            List<MatchInfo> templateOneDispositions = new List<MatchInfo>();
            templateOneDispositions.Add(new ParameterMatchInfo("foo", "test", MatchKind.InvalidName));
            templateOneDispositions.Add(new ParameterMatchInfo("bar", "test2", MatchKind.InvalidName));
            ITemplateMatchInfo templateOneMatchInfo = new TemplateMatchInfo2(new MockTemplateInfo(), templateOneDispositions);
            matchInfo.Add(templateOneMatchInfo);

            // template two
            List<MatchInfo> templateTwoDispositions = new List<MatchInfo>();
            templateTwoDispositions.Add(new ParameterMatchInfo("foo", "test", MatchKind.InvalidName));
            ITemplateMatchInfo templateTwoMatchInfo = new TemplateMatchInfo2(new MockTemplateInfo(), templateTwoDispositions);
            matchInfo.Add(templateTwoMatchInfo);

            // template three
            List<MatchInfo> templateThreeDispositions = new List<MatchInfo>();
            templateThreeDispositions.Add(new ParameterMatchInfo("foo", "test", MatchKind.InvalidName));
            templateThreeDispositions.Add(new ParameterMatchInfo("baz", "test3", MatchKind.InvalidName));
            ITemplateMatchInfo templateThreeMatchInfo = new TemplateMatchInfo2(new MockTemplateInfo(), templateThreeDispositions);
            matchInfo.Add(templateThreeMatchInfo);

            HelpForTemplateResolution.GetParametersInvalidForTemplatesInList(matchInfo, out IReadOnlyList<string> invalidForAllTemplates, out IReadOnlyList<string> invalidForSomeTemplates);

            Assert.Equal(1, invalidForAllTemplates.Count);
            Assert.Contains("foo", invalidForAllTemplates);

            Assert.Equal(2, invalidForSomeTemplates.Count);
            Assert.Contains("bar", invalidForSomeTemplates);
            Assert.Contains("baz", invalidForSomeTemplates);
        }

        [Fact(DisplayName = nameof(GetParametersInvalidForTemplatesInList_NoneForAllTest))]
        public void GetParametersInvalidForTemplatesInList_NoneForAllTest()
        {
            List<ITemplateMatchInfo> matchInfo = new List<ITemplateMatchInfo>();

            // template one
            List<MatchInfo> templateOneDispositions = new List<MatchInfo>();
            templateOneDispositions.Add(new ParameterMatchInfo("foo", "test", MatchKind.InvalidName));
            templateOneDispositions.Add(new ParameterMatchInfo("bar", "test2", MatchKind.InvalidName));
            ITemplateMatchInfo templateOneMatchInfo = new TemplateMatchInfo2(new MockTemplateInfo(), templateOneDispositions);
            matchInfo.Add(templateOneMatchInfo);

            // template two
            List<MatchInfo> templateTwoDispositions = new List<MatchInfo>();
            ITemplateMatchInfo templateTwoMatchInfo = new TemplateMatchInfo2(new MockTemplateInfo(), templateTwoDispositions);
            matchInfo.Add(templateTwoMatchInfo);

            HelpForTemplateResolution.GetParametersInvalidForTemplatesInList(matchInfo, out IReadOnlyList<string> invalidForAllTemplates, out IReadOnlyList<string> invalidForSomeTemplates);

            Assert.Equal(0, invalidForAllTemplates.Count);

            Assert.Equal(2, invalidForSomeTemplates.Count);
            Assert.Contains("foo", invalidForSomeTemplates);
            Assert.Contains("bar", invalidForSomeTemplates);
        }

        [Fact(DisplayName = nameof(GetParametersInvalidForTemplatesInList_NoneForSomeTest))]
        public void GetParametersInvalidForTemplatesInList_NoneForSomeTest()
        {
            List<ITemplateMatchInfo> matchInfo = new List<ITemplateMatchInfo>();

            // template one
            List<MatchInfo> templateOneDispositions = new List<MatchInfo>();
            templateOneDispositions.Add(new ParameterMatchInfo("foo", "test", MatchKind.InvalidName));
            ITemplateMatchInfo templateOneMatchInfo = new TemplateMatchInfo2(new MockTemplateInfo(), templateOneDispositions);
            matchInfo.Add(templateOneMatchInfo);

            // template two
            List<MatchInfo> templateTwoDispositions = new List<MatchInfo>();
            templateTwoDispositions.Add(new ParameterMatchInfo("foo", "test", MatchKind.InvalidName));
            ITemplateMatchInfo templateTwoMatchInfo = new TemplateMatchInfo2(new MockTemplateInfo(), templateTwoDispositions);
            matchInfo.Add(templateTwoMatchInfo);

            HelpForTemplateResolution.GetParametersInvalidForTemplatesInList(matchInfo, out IReadOnlyList<string> invalidForAllTemplates, out IReadOnlyList<string> invalidForSomeTemplates);

            Assert.Equal(1, invalidForAllTemplates.Count);
            Assert.Contains("foo", invalidForAllTemplates);

            Assert.Equal(0, invalidForSomeTemplates.Count);
        }
    }
}
