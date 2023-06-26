// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FakeItEasy;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Parameters;
using Microsoft.TemplateEngine.Utils;

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
            A.CallTo(() => templateInfo.ParameterDefinitions).Returns(new ParameterDefinitionSet(new List<ITemplateParameter>() { param }));
            Dictionary<string, string?> parameterValues = new()
            {
                { "TestName", "whatever" }
            };

            string? telemetryEntry = TelemetryHelper.PrepareHashedChoiceValue(templateInfo, parameterValues, "TestName");
            Assert.Null(telemetryEntry);
        }

        [Fact(DisplayName = nameof(UnknownParameterNameHasNullCanonicalValueTest))]
        public void UnknownParameterNameHasNullCanonicalValueTest()
        {
            ITemplateParameter param = new TemplateParameter("TestName", type: "parameter", datatype: "string", choices: null);
            IReadOnlyList<ITemplateParameter> parametersForTemplate = new List<ITemplateParameter>() { param };

            ITemplateInfo templateInfo = A.Fake<ITemplateInfo>();
            A.CallTo(() => templateInfo.ParameterDefinitions).Returns(new ParameterDefinitionSet(new List<ITemplateParameter>() { param }));

            Dictionary<string, string?> parameterValues = new()
            {
                { "TestName", "whatever" }
            };

            string? telemetryEntry = TelemetryHelper.PrepareHashedChoiceValue(templateInfo, parameterValues, "OtherName");
            Assert.Null(telemetryEntry);
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
            A.CallTo(() => templateInfo.ParameterDefinitions).Returns(new ParameterDefinitionSet(new List<ITemplateParameter>() { param }));
            Dictionary<string, string?> parameterValues = new()
            {
                { "TestName", "whatever" }
            };

            string? telemetryEntry = TelemetryHelper.PrepareHashedChoiceValue(templateInfo, parameterValues, "TestName");
            Assert.Null(telemetryEntry);
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
            A.CallTo(() => templateInfo.ParameterDefinitions).Returns(new ParameterDefinitionSet(new List<ITemplateParameter>() { param }));
            Dictionary<string, string?> parameterValues = new()
            {
                { "TestName", "foo" }
            };

            string? telemetryEntry = TelemetryHelper.PrepareHashedChoiceValue(templateInfo, parameterValues, "TestName");
            Assert.Equal(Sha256Hasher.HashWithNormalizedCasing("foo"), telemetryEntry);
        }

        [Fact]
        public void UniqueStartsWithValueDoesNotResolveCanonicalValueTest()
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
            A.CallTo(() => templateInfo.ParameterDefinitions).Returns(new ParameterDefinitionSet(new List<ITemplateParameter>() { param }));
            Dictionary<string, string?> parameterValues = new()
            {
                { "TestName", "f" }
            };

            string? telemetryEntry = TelemetryHelper.PrepareHashedChoiceValue(templateInfo, parameterValues, "TestName");
            Assert.Null(telemetryEntry);
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
            A.CallTo(() => templateInfo.ParameterDefinitions).Returns(new ParameterDefinitionSet(new List<ITemplateParameter>() { param }));
            Dictionary<string, string?> parameterValues = new()
            {
                { "TestName", "f" }
            };

            string? telemetryEntry = TelemetryHelper.PrepareHashedChoiceValue(templateInfo, parameterValues, "TestName");
            Assert.Null(telemetryEntry);
        }

        [Fact(DisplayName = nameof(ChoiceValueCaseDifferenceIsAMatchTest))]
        public void ChoiceValueCaseDifferenceIsAMatchTest()
        {
            ITemplateParameter param = new TemplateParameter(
                name: "TestName",
                type: "parameter",
                datatype: "choice",
                choices: new Dictionary<string, ParameterChoice>(StringComparer.OrdinalIgnoreCase)
                {
                    { "foo", new ParameterChoice("Foo", "Foo value") },
                    { "bar", new ParameterChoice("Bar", "Bar value") }
                });
            IReadOnlyList<ITemplateParameter> parametersForTemplate = new List<ITemplateParameter>() { param };
            ITemplateInfo templateInfo = A.Fake<ITemplateInfo>();
            A.CallTo(() => templateInfo.ParameterDefinitions).Returns(new ParameterDefinitionSet(new List<ITemplateParameter>() { param }));
            Dictionary<string, string?> parameterValues = new()
            {
                { "TestName", "FOO" }
            };

            string? telemetryEntry = TelemetryHelper.PrepareHashedChoiceValue(templateInfo, parameterValues, "TestName");
            Assert.Equal(Sha256Hasher.HashWithNormalizedCasing("FOO"), telemetryEntry);
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
            A.CallTo(() => templateInfo.ParameterDefinitions).Returns(new ParameterDefinitionSet(new List<ITemplateParameter>() { param }));
            Dictionary<string, string?> parameterValues = new()
            {
                { "TestName", "foo" }
            };

            string? telemetryEntry = TelemetryHelper.PrepareHashedChoiceValue(templateInfo, parameterValues, "TestName");
            Assert.Null(telemetryEntry);
        }

        [Fact]
        public void MultiValueChoiceTest()
        {
            ITemplateParameter param = new TemplateParameter(
                  name: "TestName",
                  type: "parameter",
                  datatype: "choice",
                  choices: new Dictionary<string, ParameterChoice>()
                  {
                        { "foo", new ParameterChoice("Foo", "Foo value") },
                        { "bar", new ParameterChoice("Bar", "Bar value") },
                        { "baz", new ParameterChoice("Baz", "Baz value") },
                  });

            IReadOnlyList<ITemplateParameter> parametersForTemplate = new List<ITemplateParameter>() { param };
            ITemplateInfo templateInfo = A.Fake<ITemplateInfo>();
            A.CallTo(() => templateInfo.ParameterDefinitions).Returns(new ParameterDefinitionSet(new List<ITemplateParameter>() { param }));
            Dictionary<string, string?> parameterValues = new()
            {
                { "TestName", "foo|bar" }
            };

            string? telemetryEntry = TelemetryHelper.PrepareHashedChoiceValue(templateInfo, parameterValues, "TestName");
            Assert.Equal(Sha256Hasher.HashWithNormalizedCasing("foo") + ";" + Sha256Hasher.HashWithNormalizedCasing("bar"), telemetryEntry);
        }

        [Fact]
        public void MultiValueChoice_SkipsInvalidEntriesTest()
        {
            ITemplateParameter param = new TemplateParameter(
                  name: "TestName",
                  type: "parameter",
                  datatype: "choice",
                  choices: new Dictionary<string, ParameterChoice>()
                  {
                        { "foo", new ParameterChoice("Foo", "Foo value") },
                        { "bar", new ParameterChoice("Bar", "Bar value") },
                        { "baz", new ParameterChoice("Baz", "Baz value") },
                  });

            IReadOnlyList<ITemplateParameter> parametersForTemplate = new List<ITemplateParameter>() { param };
            ITemplateInfo templateInfo = A.Fake<ITemplateInfo>();
            A.CallTo(() => templateInfo.ParameterDefinitions).Returns(new ParameterDefinitionSet(new List<ITemplateParameter>() { param }));
            Dictionary<string, string?> parameterValues = new()
            {
                { "TestName", "foo|unknown|bar" }
            };

            string? telemetryEntry = TelemetryHelper.PrepareHashedChoiceValue(templateInfo, parameterValues, "TestName");
            Assert.Equal(Sha256Hasher.HashWithNormalizedCasing("foo") + ";" + Sha256Hasher.HashWithNormalizedCasing("bar"), telemetryEntry);
        }
    }
}
