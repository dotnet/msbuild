using System.Collections.Generic;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Cli.HelpAndUsage;
using Xunit;
using Microsoft.TemplateEngine.Mocks;

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
            templateOneDispositions.Add(new MatchInfo() { Location = MatchLocation.OtherParameter, Kind = MatchKind.InvalidParameterName, InputParameterName = "foo" });
            templateOneDispositions.Add(new MatchInfo() { Location = MatchLocation.OtherParameter, Kind = MatchKind.InvalidParameterName, InputParameterName = "bar" });
            ITemplateMatchInfo templateOneMatchInfo = new TemplateMatchInfo(new MockTemplateInfo(), templateOneDispositions);
            matchInfo.Add(templateOneMatchInfo);

            // template two
            List<MatchInfo> templateTwoDispositions = new List<MatchInfo>();
            templateTwoDispositions.Add(new MatchInfo() { Location = MatchLocation.OtherParameter, Kind = MatchKind.InvalidParameterName, InputParameterName = "foo" });
            ITemplateMatchInfo templateTwoMatchInfo = new TemplateMatchInfo(new MockTemplateInfo(), templateTwoDispositions);
            matchInfo.Add(templateTwoMatchInfo);

            // template three
            List<MatchInfo> templateThreeDispositions = new List<MatchInfo>();
            templateThreeDispositions.Add(new MatchInfo() { Location = MatchLocation.OtherParameter, Kind = MatchKind.InvalidParameterName, InputParameterName = "foo" });
            templateThreeDispositions.Add(new MatchInfo() { Location = MatchLocation.OtherParameter, Kind = MatchKind.InvalidParameterName, InputParameterName = "baz" });
            ITemplateMatchInfo templateThreeMatchInfo = new TemplateMatchInfo(new MockTemplateInfo(), templateThreeDispositions);
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
            templateOneDispositions.Add(new MatchInfo() { Location = MatchLocation.OtherParameter, Kind = MatchKind.InvalidParameterName, InputParameterName = "foo" });
            templateOneDispositions.Add(new MatchInfo() { Location = MatchLocation.OtherParameter, Kind = MatchKind.InvalidParameterName, InputParameterName = "bar" });
            ITemplateMatchInfo templateOneMatchInfo = new TemplateMatchInfo(new MockTemplateInfo(), templateOneDispositions);
            matchInfo.Add(templateOneMatchInfo);

            // template two
            List<MatchInfo> templateTwoDispositions = new List<MatchInfo>();
            ITemplateMatchInfo templateTwoMatchInfo = new TemplateMatchInfo(new MockTemplateInfo(), templateTwoDispositions);
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
            templateOneDispositions.Add(new MatchInfo() { Location = MatchLocation.OtherParameter, Kind = MatchKind.InvalidParameterName, InputParameterName = "foo" });
            ITemplateMatchInfo templateOneMatchInfo = new TemplateMatchInfo(new MockTemplateInfo(), templateOneDispositions);
            matchInfo.Add(templateOneMatchInfo);

            // template two
            List<MatchInfo> templateTwoDispositions = new List<MatchInfo>();
            templateTwoDispositions.Add(new MatchInfo() { Location = MatchLocation.OtherParameter, Kind = MatchKind.InvalidParameterName, InputParameterName = "foo" });
            ITemplateMatchInfo templateTwoMatchInfo = new TemplateMatchInfo(new MockTemplateInfo(), templateTwoDispositions);
            matchInfo.Add(templateTwoMatchInfo);

            HelpForTemplateResolution.GetParametersInvalidForTemplatesInList(matchInfo, out IReadOnlyList<string> invalidForAllTemplates, out IReadOnlyList<string> invalidForSomeTemplates);

            Assert.Equal(1, invalidForAllTemplates.Count);
            Assert.Contains("foo", invalidForAllTemplates);

            Assert.Equal(0, invalidForSomeTemplates.Count);
        }
    }
}
