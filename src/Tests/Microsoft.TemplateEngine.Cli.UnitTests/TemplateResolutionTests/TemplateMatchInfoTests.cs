// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class TemplateMatchInfoTests
    {
        [Fact(DisplayName = nameof(EmptyMatchDisposition_ReportsCorrectly))]
        public void EmptyMatchDisposition_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo templateMatchInfo = new TemplateMatchInfo(templateInfo);
            Assert.False(WellKnownSearchFilters.MatchesAllCriteria(templateMatchInfo));
            Assert.False(WellKnownSearchFilters.MatchesAtLeastOneCriteria(templateMatchInfo));
            Assert.Empty(templateMatchInfo.GetInvalidParameterNames());
            Assert.Equal(0, templateMatchInfo.GetValidTemplateParameters().Count);
        }

        [Fact(DisplayName = nameof(NameExactMatch_ReportsCorrectly))]
        public void NameExactMatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo templateMatchInfo = new TemplateMatchInfo(templateInfo);
            templateMatchInfo.AddMatchDisposition(new MatchInfo(MatchInfo.BuiltIn.Name, "test", MatchKind.Exact));
            Assert.True(WellKnownSearchFilters.MatchesAllCriteria(templateMatchInfo));
            Assert.True(WellKnownSearchFilters.MatchesAtLeastOneCriteria(templateMatchInfo));
        }

        [Fact(DisplayName = nameof(NamePartialMatch_ReportsCorrectly))]
        public void NamePartialMatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo templateMatchInfo = new TemplateMatchInfo(templateInfo);
            templateMatchInfo.AddMatchDisposition(new MatchInfo(MatchInfo.BuiltIn.Name, "test", MatchKind.Partial));
            Assert.True(WellKnownSearchFilters.MatchesAllCriteria(templateMatchInfo));
            Assert.True(WellKnownSearchFilters.MatchesAtLeastOneCriteria(templateMatchInfo));
        }

        [Fact(DisplayName = nameof(NameMismatch_ReportsCorrectly))]
        public void NameMismatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo templateMatchInfo = new TemplateMatchInfo(templateInfo);
            templateMatchInfo.AddMatchDisposition(new MatchInfo(MatchInfo.BuiltIn.Name, "test", MatchKind.Mismatch));
            Assert.False(WellKnownSearchFilters.MatchesAllCriteria(templateMatchInfo));
            Assert.False(WellKnownSearchFilters.MatchesAtLeastOneCriteria(templateMatchInfo));
        }

        [Fact(DisplayName = nameof(TypeMatch_ReportsCorrectly))]
        public void TypeMatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo templateMatchInfo = new TemplateMatchInfo(templateInfo);
            templateMatchInfo.AddMatchDisposition(new MatchInfo(MatchInfo.BuiltIn.Type, "test", MatchKind.Exact));
            Assert.True(WellKnownSearchFilters.MatchesAllCriteria(templateMatchInfo));
            Assert.True(WellKnownSearchFilters.MatchesAtLeastOneCriteria(templateMatchInfo));
        }

        [Fact(DisplayName = nameof(TypeMismatch_ReportsCorrectly))]
        public void TypeMismatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo templateMatchInfo = new TemplateMatchInfo(templateInfo);
            templateMatchInfo.AddMatchDisposition(new MatchInfo(MatchInfo.BuiltIn.Type, "test", MatchKind.Mismatch));
            Assert.False(WellKnownSearchFilters.MatchesAllCriteria(templateMatchInfo));
            Assert.False(WellKnownSearchFilters.MatchesAtLeastOneCriteria(templateMatchInfo));
        }

        [Fact(DisplayName = nameof(TypeMatch_NameMatch_ReportsCorrectly))]
        public void TypeMatch_NameMatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo templateMatchInfo = new TemplateMatchInfo(templateInfo);
            templateMatchInfo.AddMatchDisposition(new MatchInfo(MatchInfo.BuiltIn.Name, "test", MatchKind.Exact));
            templateMatchInfo.AddMatchDisposition(new MatchInfo(MatchInfo.BuiltIn.Type, "test", MatchKind.Exact));
            Assert.True(WellKnownSearchFilters.MatchesAllCriteria(templateMatchInfo));
            Assert.True(WellKnownSearchFilters.MatchesAtLeastOneCriteria(templateMatchInfo));
        }

        [Fact(DisplayName = nameof(TypeMatch_NameMismatch_ReportsCorrectly))]
        public void TypeMatch_NameMismatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo templateMatchInfo = new TemplateMatchInfo(templateInfo);
            templateMatchInfo.AddMatchDisposition(new MatchInfo(MatchInfo.BuiltIn.Name, "test", MatchKind.Mismatch));
            templateMatchInfo.AddMatchDisposition(new MatchInfo(MatchInfo.BuiltIn.Type, "test", MatchKind.Exact));
            Assert.False(WellKnownSearchFilters.MatchesAllCriteria(templateMatchInfo));
            Assert.True(WellKnownSearchFilters.MatchesAtLeastOneCriteria(templateMatchInfo));
        }

        [Fact(DisplayName = nameof(TypeMatch_NamePartialMatch_ReportsCorrectly))]
        public void TypeMatch_NamePartialMatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo templateMatchInfo = new TemplateMatchInfo(templateInfo);
            templateMatchInfo.AddMatchDisposition(new MatchInfo(MatchInfo.BuiltIn.Name, "test", MatchKind.Partial));
            templateMatchInfo.AddMatchDisposition(new MatchInfo(MatchInfo.BuiltIn.Type, "test", MatchKind.Exact));
            Assert.True(WellKnownSearchFilters.MatchesAllCriteria(templateMatchInfo));
            Assert.True(WellKnownSearchFilters.MatchesAtLeastOneCriteria(templateMatchInfo));
        }

        [Fact(DisplayName = nameof(TypeMismatch_NameMatch_ReportsCorrectly))]
        public void TypeMismatch_NameMatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo templateMatchInfo = new TemplateMatchInfo(templateInfo);
            templateMatchInfo.AddMatchDisposition(new MatchInfo(MatchInfo.BuiltIn.Name, "test", MatchKind.Exact));
            templateMatchInfo.AddMatchDisposition(new MatchInfo(MatchInfo.BuiltIn.Type, "test", MatchKind.Mismatch));
            Assert.False(WellKnownSearchFilters.MatchesAllCriteria(templateMatchInfo));
            Assert.True(WellKnownSearchFilters.MatchesAtLeastOneCriteria(templateMatchInfo));
        }

        [Fact(DisplayName = nameof(TypeMismatch_NamePartialMatch_ReportsCorrectly))]
        public void TypeMismatch_NamePartialMatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo templateMatchInfo = new TemplateMatchInfo(templateInfo);
            templateMatchInfo.AddMatchDisposition(new MatchInfo(MatchInfo.BuiltIn.Name, "test", MatchKind.Partial));
            templateMatchInfo.AddMatchDisposition(new MatchInfo(MatchInfo.BuiltIn.Type, "test", MatchKind.Mismatch));
            Assert.False(WellKnownSearchFilters.MatchesAllCriteria(templateMatchInfo));
            Assert.True(WellKnownSearchFilters.MatchesAtLeastOneCriteria(templateMatchInfo));
        }
    }
}
