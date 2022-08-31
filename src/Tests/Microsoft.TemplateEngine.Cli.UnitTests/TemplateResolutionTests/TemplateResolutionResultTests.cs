// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Cli.TemplateResolution;

namespace Microsoft.TemplateEngine.Cli.UnitTests.TemplateResolutionTests
{
    public class TemplateResolutionResultTests
    {
        [Fact]
        public void GetAllMatchedParametersList_Basic()
        {
            var templateMatchInfo = A.Fake<ITemplateMatchInfo>();

            A.CallTo(() => templateMatchInfo.MatchDisposition).Returns(
                new[]
                {
                    new ParameterMatchInfo("param", "paramValue", MatchKind.Mismatch, ParameterMatchInfo.MismatchKind.InvalidValue, "--param")
                });

            var parameters = TemplateResolutionResult.GetAllMatchedParametersList(new[] { templateMatchInfo });

            Assert.Equal(1, parameters.Count);
            Assert.Equal("paramValue", parameters["--param"]);
        }

        [Fact]
        public void GetAllMatchedParametersList_FallbackToName()
        {
            var templateMatchInfo = A.Fake<ITemplateMatchInfo>();

            A.CallTo(() => templateMatchInfo.MatchDisposition).Returns(
                new[]
                {
                    new ParameterMatchInfo("param", "paramValue", MatchKind.Mismatch, ParameterMatchInfo.MismatchKind.InvalidValue, null)
                });

            var parameters = TemplateResolutionResult.GetAllMatchedParametersList(new[] { templateMatchInfo });

            Assert.Equal(1, parameters.Count);
            Assert.Equal("paramValue", parameters["param"]);
        }

        [Fact]
        public void GetAllMatchedParametersList_PreservesValueIfGiven()
        {
            var templateMatchInfo = A.Fake<ITemplateMatchInfo>();

            A.CallTo(() => templateMatchInfo.MatchDisposition).Returns(
                new[]
                {
                    new ParameterMatchInfo("param", "paramValue", MatchKind.Mismatch, ParameterMatchInfo.MismatchKind.InvalidValue, "--param")
                });

            var templateMatchInfo2 = A.Fake<ITemplateMatchInfo>();

            A.CallTo(() => templateMatchInfo2.MatchDisposition).Returns(
                new[]
                {
                    new ParameterMatchInfo("param", null, MatchKind.Mismatch, ParameterMatchInfo.MismatchKind.InvalidValue, "--param")
                });

            var parameters = TemplateResolutionResult.GetAllMatchedParametersList(new[] { templateMatchInfo, templateMatchInfo2 });
            Assert.Equal(1, parameters.Count);
            Assert.Equal("paramValue", parameters["--param"]);

            parameters = TemplateResolutionResult.GetAllMatchedParametersList(new[] { templateMatchInfo2, templateMatchInfo });
            Assert.Equal(1, parameters.Count);
            Assert.Equal("paramValue", parameters["--param"]);
        }

        [Fact]
        public void GetAllMatchedParametersList_IgnoresNonParameterMatches()
        {
            var templateMatchInfo = A.Fake<ITemplateMatchInfo>();

            A.CallTo(() => templateMatchInfo.MatchDisposition).Returns(
                new[]
                {
                    new MatchInfo("language", "C#", MatchKind.Exact),
                    new ParameterMatchInfo("param", "paramValue", MatchKind.Mismatch, ParameterMatchInfo.MismatchKind.InvalidValue, "--param")
                });

            var parameters = TemplateResolutionResult.GetAllMatchedParametersList(new[] { templateMatchInfo });
            Assert.Equal(1, parameters.Count);
            Assert.Equal("paramValue", parameters["--param"]);
        }

        [Fact]
        public void GetAllMatchedParametersList_DoesNotDependOnMatchKind()
        {
            var templateMatchInfo = A.Fake<ITemplateMatchInfo>();

            A.CallTo(() => templateMatchInfo.MatchDisposition).Returns(
                new[]
                {
                    new ParameterMatchInfo("param", "paramValue", MatchKind.Exact, ParameterMatchInfo.MismatchKind.NoMismatch, "--param"),
                    new ParameterMatchInfo("param2", "paramValue2", MatchKind.Mismatch, ParameterMatchInfo.MismatchKind.InvalidValue, "--param2"),
                    new ParameterMatchInfo("param3", "paramValue3", MatchKind.Partial, ParameterMatchInfo.MismatchKind.NoMismatch, "--param3")
                });

            var parameters = TemplateResolutionResult.GetAllMatchedParametersList(new[] { templateMatchInfo });
            Assert.Equal(3, parameters.Count);
            Assert.Equal("paramValue", parameters["--param"]);
            Assert.Equal("paramValue2", parameters["--param2"]);
            Assert.Equal("paramValue3", parameters["--param3"]);
        }
    }
}
