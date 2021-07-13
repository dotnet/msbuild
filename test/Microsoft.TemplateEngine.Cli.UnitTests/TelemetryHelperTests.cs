// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class TelemetryHelperTests
    {
        [Fact(DisplayName = nameof(NonChoiceParameterHasNullCanonicalValueTest))]
        public void NonChoiceParameterHasNullCanonicalValueTest()
        {
            ITemplateParameter param = new TemplateParameter("TestName", type: "parameter", datatype: "string", choices: null);
            IReadOnlyList<ITemplateParameter> parametersForTemplate = new List<ITemplateParameter>() { param };

            ITemplateInfo templateInfo = A.Fake<ITemplateInfo>();
            A.CallTo(() => templateInfo.Parameters).Returns(new List<ITemplateParameter>() { param });

            string canonical = TelemetryHelper.GetCanonicalValueForChoiceParamOrDefault(templateInfo, "TestName", "whatever");
            Assert.Null(canonical);
        }

        [Fact(DisplayName = nameof(UnknownParameterNameHasNullCanonicalValueTest))]
        public void UnknownParameterNameHasNullCanonicalValueTest()
        {
            ITemplateParameter param = new TemplateParameter("TestName", type: "parameter", datatype: "string", choices: null);
            IReadOnlyList<ITemplateParameter> parametersForTemplate = new List<ITemplateParameter>() { param };

            ITemplateInfo templateInfo = A.Fake<ITemplateInfo>();
            A.CallTo(() => templateInfo.Parameters).Returns(new List<ITemplateParameter>() { param });

            string canonical = TelemetryHelper.GetCanonicalValueForChoiceParamOrDefault(templateInfo, "OtherName", "whatever");
            Assert.Null(canonical);
        }

        [Fact(DisplayName = nameof(InvalidChoiceValueForParameterHasNullCanonicalValueTest))]
        public void InvalidChoiceValueForParameterHasNullCanonicalValueTest()
        {
            ITemplateParameter param = new TemplateParameter(
                name: "TestName",
                type: "parameter",
                datatype: "choice",
                choices: new Dictionary<string, ParameterChoice>()
                {
                    { "foo", new ParameterChoice("Foo", "Foo value") },
                    { "bar", new ParameterChoice("Bar", "Bar value") }
                });

            IReadOnlyList<ITemplateParameter> parametersForTemplate = new List<ITemplateParameter>() { param };
            ITemplateInfo templateInfo = A.Fake<ITemplateInfo>();
            A.CallTo(() => templateInfo.Parameters).Returns(new List<ITemplateParameter>() { param });

            string canonical = TelemetryHelper.GetCanonicalValueForChoiceParamOrDefault(templateInfo, "TestName", "whatever");
            Assert.Null(canonical);
        }

        [Fact(DisplayName = nameof(ValidChoiceForParameterIsItsOwnCanonicalValueTest))]
        public void ValidChoiceForParameterIsItsOwnCanonicalValueTest()
        {
            ITemplateParameter param = new TemplateParameter(
                name: "TestName",
                type: "parameter",
                datatype: "choice",
                choices: new Dictionary<string, ParameterChoice>()
                {
                    { "foo", new ParameterChoice("Foo", "Foo value") },
                    { "bar", new ParameterChoice("Bar", "Bar value") }
                });

            IReadOnlyList<ITemplateParameter> parametersForTemplate = new List<ITemplateParameter>() { param };
            ITemplateInfo templateInfo = A.Fake<ITemplateInfo>();
            A.CallTo(() => templateInfo.Parameters).Returns(new List<ITemplateParameter>() { param });

            string canonical = TelemetryHelper.GetCanonicalValueForChoiceParamOrDefault(templateInfo, "TestName", "foo");
            Assert.Equal("foo", canonical);
        }

        [Fact(DisplayName = nameof(UniqueStartsWithValueResolvesCanonicalValueTest))]
        public void UniqueStartsWithValueResolvesCanonicalValueTest()
        {
            ITemplateParameter param = new TemplateParameter(
                name: "TestName",
                type: "parameter",
                datatype: "choice",
                choices: new Dictionary<string, ParameterChoice>()
                {
                    { "foo", new ParameterChoice("Foo", "Foo value") },
                    { "bar", new ParameterChoice("Bar", "Bar value") }
                });
            IReadOnlyList<ITemplateParameter> parametersForTemplate = new List<ITemplateParameter>() { param };
            ITemplateInfo templateInfo = A.Fake<ITemplateInfo>();
            A.CallTo(() => templateInfo.Parameters).Returns(new List<ITemplateParameter>() { param });

            string canonical = TelemetryHelper.GetCanonicalValueForChoiceParamOrDefault(templateInfo, "TestName", "f");
            Assert.Equal("foo", canonical);
        }

        [Fact(DisplayName = nameof(AmbiguousStartsWithValueHasNullCanonicalValueTest))]
        public void AmbiguousStartsWithValueHasNullCanonicalValueTest()
        {
            ITemplateParameter param = new TemplateParameter(
                name: "TestName",
                type: "parameter",
                datatype: "choice",
                choices: new Dictionary<string, ParameterChoice>()
                {
                        { "foo", new ParameterChoice("Foo", "Foo value") },
                        { "bar", new ParameterChoice("Bar", "Bar value") },
                        { "foot", new ParameterChoice("Foot", "Foot value") }
                });
            IReadOnlyList<ITemplateParameter> parametersForTemplate = new List<ITemplateParameter>() { param };

            ITemplateInfo templateInfo = A.Fake<ITemplateInfo>();
            A.CallTo(() => templateInfo.Parameters).Returns(new List<ITemplateParameter>() { param });

            string canonical = TelemetryHelper.GetCanonicalValueForChoiceParamOrDefault(templateInfo, "TestName", "f");
            Assert.Null(canonical);
        }

        [Fact(DisplayName = nameof(ChoiceValueCaseDifferenceIsAMatchTest))]
        public void ChoiceValueCaseDifferenceIsAMatchTest()
        {
            ITemplateParameter param = new TemplateParameter(
                name: "TestName",
                type: "parameter",
                datatype: "choice",
                choices: new Dictionary<string, ParameterChoice>()
                {
                    { "foo", new ParameterChoice("Foo", "Foo value") },
                    { "bar", new ParameterChoice("Bar", "Bar value") }
                });
            IReadOnlyList<ITemplateParameter> parametersForTemplate = new List<ITemplateParameter>() { param };
            ITemplateInfo templateInfo = A.Fake<ITemplateInfo>();
            A.CallTo(() => templateInfo.Parameters).Returns(new List<ITemplateParameter>() { param });

            string canonical = TelemetryHelper.GetCanonicalValueForChoiceParamOrDefault(templateInfo, "TestName", "FOO");
            Assert.Equal("foo", canonical);
        }

        [Fact(DisplayName = nameof(ChoiceValueCaseDifferencesContributeToAmbiguousMatchTest))]
        public void ChoiceValueCaseDifferencesContributeToAmbiguousMatchTest()
        {
            ITemplateParameter param = new TemplateParameter(
                  name: "TestName",
                  type: "parameter",
                  datatype: "choice",
                  choices: new Dictionary<string, ParameterChoice>()
                  {
                        { "foot", new ParameterChoice("Foo", "Foo value") },
                        { "bar", new ParameterChoice("Bar", "Bar value") },
                        { "Football", new ParameterChoice("Football", "Foo value") },
                        { "FOOTPOUND", new ParameterChoice("Footpound", "Foo value") }
                  });
            IReadOnlyList<ITemplateParameter> parametersForTemplate = new List<ITemplateParameter>() { param };
            ITemplateInfo templateInfo = A.Fake<ITemplateInfo>();
            A.CallTo(() => templateInfo.Parameters).Returns(new List<ITemplateParameter>() { param });

            string canonical = TelemetryHelper.GetCanonicalValueForChoiceParamOrDefault(templateInfo, "TestName", "foo");
            Assert.Null(canonical);
        }
    }
}
