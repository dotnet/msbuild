using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Mocks;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class TemplateMatchInfoTests
    {
        [Fact(DisplayName = nameof(EmptyMatchDisposition_ReportsCorrectly))]
        public void EmptyMatchDisposition_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo TemplateMatchInfo = new TemplateMatchInfo(templateInfo);
            Assert.False(TemplateMatchInfo.IsMatch);
            Assert.False(TemplateMatchInfo.IsMatchExceptContext());
            Assert.False(TemplateMatchInfo.IsPartialMatch);
            Assert.False(TemplateMatchInfo.IsPartialMatchExceptContext());
            Assert.False(TemplateMatchInfo.IsInvokableMatch());
            Assert.False(TemplateMatchInfo.HasAmbiguousParameterValueMatch());
            Assert.False(TemplateMatchInfo.HasParameterMismatch());
            Assert.False(TemplateMatchInfo.HasParseError());
            Assert.Equal(0, TemplateMatchInfo.GetInvalidParameterNames().Count);
            Assert.Equal(0, TemplateMatchInfo.GetValidTemplateParameters().Count);
        }

        [Fact(DisplayName = nameof(NameExactMatch_ReportsCorrectly))]
        public void NameExactMatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo TemplateMatchInfo = new TemplateMatchInfo(templateInfo);
            TemplateMatchInfo.AddDisposition(new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Exact });
            Assert.True(TemplateMatchInfo.IsMatch);
            Assert.False(TemplateMatchInfo.IsMatchExceptContext());
            Assert.True(TemplateMatchInfo.IsPartialMatch);
            Assert.False(TemplateMatchInfo.IsPartialMatchExceptContext());
            Assert.True(TemplateMatchInfo.IsInvokableMatch());
            Assert.False(TemplateMatchInfo.HasAmbiguousParameterValueMatch());
            Assert.False(TemplateMatchInfo.HasParameterMismatch());
            Assert.False(TemplateMatchInfo.HasParseError());
        }

        [Fact(DisplayName = nameof(NamePartialMatch_ReportsCorrectly))]
        public void NamePartialMatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo TemplateMatchInfo = new TemplateMatchInfo(templateInfo);
            TemplateMatchInfo.AddDisposition(new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Partial });
            Assert.True(TemplateMatchInfo.IsMatch);
            Assert.False(TemplateMatchInfo.IsMatchExceptContext());
            Assert.True(TemplateMatchInfo.IsPartialMatch);
            Assert.False(TemplateMatchInfo.IsPartialMatchExceptContext());
            Assert.True(TemplateMatchInfo.IsInvokableMatch());
            Assert.False(TemplateMatchInfo.HasAmbiguousParameterValueMatch());
            Assert.False(TemplateMatchInfo.HasParameterMismatch());
            Assert.False(TemplateMatchInfo.HasParseError());
        }

        [Fact(DisplayName = nameof(NameMismatch_ReportsCorrectly))]
        public void NameMismatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo TemplateMatchInfo = new TemplateMatchInfo(templateInfo);
            TemplateMatchInfo.AddDisposition(new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Mismatch });
            Assert.False(TemplateMatchInfo.IsMatch);
            Assert.False(TemplateMatchInfo.IsMatchExceptContext());
            Assert.False(TemplateMatchInfo.IsPartialMatch);
            Assert.False(TemplateMatchInfo.IsPartialMatchExceptContext());
            Assert.False(TemplateMatchInfo.IsInvokableMatch());
            Assert.False(TemplateMatchInfo.HasAmbiguousParameterValueMatch());
            Assert.False(TemplateMatchInfo.HasParameterMismatch());
            Assert.False(TemplateMatchInfo.HasParseError());
        }

        [Fact(DisplayName = nameof(ContextMatch_ReportsCorrectly))]
        public void ContextMatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo TemplateMatchInfo = new TemplateMatchInfo(templateInfo);
            TemplateMatchInfo.AddDisposition(new MatchInfo { Location = MatchLocation.Context, Kind = MatchKind.Exact });
            Assert.True(TemplateMatchInfo.IsMatch);
            Assert.False(TemplateMatchInfo.IsMatchExceptContext());
            Assert.True(TemplateMatchInfo.IsPartialMatch);
            Assert.False(TemplateMatchInfo.IsPartialMatchExceptContext());
            Assert.True(TemplateMatchInfo.IsInvokableMatch());
            Assert.False(TemplateMatchInfo.HasAmbiguousParameterValueMatch());
            Assert.False(TemplateMatchInfo.HasParameterMismatch());
            Assert.False(TemplateMatchInfo.HasParseError());
        }

        [Fact(DisplayName = nameof(ContextMismatch_ReportsCorrectly))]
        public void ContextMismatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo TemplateMatchInfo = new TemplateMatchInfo(templateInfo);
            TemplateMatchInfo.AddDisposition(new MatchInfo { Location = MatchLocation.Context, Kind = MatchKind.Mismatch });
            Assert.False(TemplateMatchInfo.IsMatch);
            Assert.True(TemplateMatchInfo.IsMatchExceptContext());
            Assert.False(TemplateMatchInfo.IsPartialMatch);
            Assert.False(TemplateMatchInfo.IsPartialMatchExceptContext()); // there must be another match info for this to be true
            Assert.False(TemplateMatchInfo.IsInvokableMatch());
            Assert.False(TemplateMatchInfo.HasAmbiguousParameterValueMatch());
            Assert.False(TemplateMatchInfo.HasParameterMismatch());
            Assert.False(TemplateMatchInfo.HasParseError());
        }

        [Fact(DisplayName = nameof(ContextMatch_NameMatch_ReportsCorrectly))]
        public void ContextMatch_NameMatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo TemplateMatchInfo = new TemplateMatchInfo(templateInfo);
            TemplateMatchInfo.AddDisposition(new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Exact });
            TemplateMatchInfo.AddDisposition(new MatchInfo { Location = MatchLocation.Context, Kind = MatchKind.Exact });
            Assert.True(TemplateMatchInfo.IsMatch);
            Assert.False(TemplateMatchInfo.IsMatchExceptContext());
            Assert.True(TemplateMatchInfo.IsPartialMatch);
            Assert.False(TemplateMatchInfo.IsPartialMatchExceptContext());
            Assert.True(TemplateMatchInfo.IsInvokableMatch());
            Assert.False(TemplateMatchInfo.HasAmbiguousParameterValueMatch());
            Assert.False(TemplateMatchInfo.HasParameterMismatch());
            Assert.False(TemplateMatchInfo.HasParseError());
        }

        [Fact(DisplayName = nameof(ContextMatch_NameMismatch_ReportsCorrectly))]
        public void ContextMatch_NameMismatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo TemplateMatchInfo = new TemplateMatchInfo(templateInfo);
            TemplateMatchInfo.AddDisposition(new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Mismatch });
            TemplateMatchInfo.AddDisposition(new MatchInfo { Location = MatchLocation.Context, Kind = MatchKind.Exact });
            Assert.False(TemplateMatchInfo.IsMatch);
            Assert.False(TemplateMatchInfo.IsMatchExceptContext());
            Assert.True(TemplateMatchInfo.IsPartialMatch);
            Assert.False(TemplateMatchInfo.IsPartialMatchExceptContext());
            Assert.False(TemplateMatchInfo.IsInvokableMatch());
            Assert.False(TemplateMatchInfo.HasAmbiguousParameterValueMatch());
            Assert.False(TemplateMatchInfo.HasParameterMismatch());
            Assert.False(TemplateMatchInfo.HasParseError());
        }

        [Fact(DisplayName = nameof(ContextMatch_NamePartialMatch_ReportsCorrectly))]
        public void ContextMatch_NamePartialMatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo TemplateMatchInfo = new TemplateMatchInfo(templateInfo);
            TemplateMatchInfo.AddDisposition(new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Partial });
            TemplateMatchInfo.AddDisposition(new MatchInfo { Location = MatchLocation.Context, Kind = MatchKind.Exact });
            Assert.True(TemplateMatchInfo.IsMatch);
            Assert.False(TemplateMatchInfo.IsMatchExceptContext());
            Assert.True(TemplateMatchInfo.IsPartialMatch);
            Assert.False(TemplateMatchInfo.IsPartialMatchExceptContext());
            Assert.True(TemplateMatchInfo.IsInvokableMatch());
            Assert.False(TemplateMatchInfo.HasAmbiguousParameterValueMatch());
            Assert.False(TemplateMatchInfo.HasParameterMismatch());
            Assert.False(TemplateMatchInfo.HasParseError());
        }

        [Fact(DisplayName = nameof(ContextMismatch_NameMatch_ReportsCorrectly))]
        public void ContextMismatch_NameMatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo TemplateMatchInfo = new TemplateMatchInfo(templateInfo);
            TemplateMatchInfo.AddDisposition(new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Exact });
            TemplateMatchInfo.AddDisposition(new MatchInfo { Location = MatchLocation.Context, Kind = MatchKind.Mismatch });
            Assert.False(TemplateMatchInfo.IsMatch);
            Assert.True(TemplateMatchInfo.IsMatchExceptContext());
            Assert.True(TemplateMatchInfo.IsPartialMatch);
            Assert.True(TemplateMatchInfo.IsPartialMatchExceptContext());
            Assert.False(TemplateMatchInfo.IsInvokableMatch());
            Assert.False(TemplateMatchInfo.HasAmbiguousParameterValueMatch());
            Assert.False(TemplateMatchInfo.HasParameterMismatch());
            Assert.False(TemplateMatchInfo.HasParseError());
        }

        [Fact(DisplayName = nameof(ContextMismatch_NamePartialMatch_ReportsCorrectly))]
        public void ContextMismatch_NamePartialMatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo TemplateMatchInfo = new TemplateMatchInfo(templateInfo);
            TemplateMatchInfo.AddDisposition(new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Partial });
            TemplateMatchInfo.AddDisposition(new MatchInfo { Location = MatchLocation.Context, Kind = MatchKind.Mismatch });
            Assert.False(TemplateMatchInfo.IsMatch);
            Assert.True(TemplateMatchInfo.IsMatchExceptContext());
            Assert.True(TemplateMatchInfo.IsPartialMatch);
            Assert.True(TemplateMatchInfo.IsPartialMatchExceptContext());
            Assert.False(TemplateMatchInfo.IsInvokableMatch());
            Assert.False(TemplateMatchInfo.HasAmbiguousParameterValueMatch());
            Assert.False(TemplateMatchInfo.HasParameterMismatch());
            Assert.False(TemplateMatchInfo.HasParseError());
        }
    }
}
