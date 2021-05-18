// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Cli.HelpAndUsage;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Cli.UnitTests.CliMocks;
using Microsoft.TemplateEngine.Mocks;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests.TemplateResolutionTests
{
    public class TemplateGroupTests
    {
        public static IEnumerable<object[]> GetInvalidParametersTestData()
        {
            yield return new object[]
            {
                new MockNewCommandInput("foo").WithTemplateOption("framework", "netcoreapp3.0"),
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group").WithChoiceParameter("framework", "netcoreapp2.1", "netcoreapp3.1"),
                    new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group").WithChoiceParameter("framework", "net5.0"),
                },
                new MockInvalidParameterInfo[]
                {
                    new MockInvalidParameterInfo(InvalidParameterInfo.Kind.InvalidParameterValue, "framework", "netcoreapp3.0")
                }
            };

            yield return new object[]
            {
                new MockNewCommandInput("foo").WithTemplateOption("fake", "netcoreapp3.0"),
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group").WithChoiceParameter("framework", "netcoreapp2.1", "netcoreapp3.1"),
                    new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group").WithChoiceParameter("framework", "net5.0"),
                },
                new MockInvalidParameterInfo[]
                {
                    new MockInvalidParameterInfo(InvalidParameterInfo.Kind.InvalidParameterName, "fake", null)
                }
            };

            yield return new object[]
            {
                new MockNewCommandInput("foo").WithTemplateOption("framework", "net"),
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group").WithChoiceParameter("framework", "netcoreapp2.1", "netcoreapp3.1"),
                    new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group").WithChoiceParameter("framework", "net5.0"),
                },
                new MockInvalidParameterInfo[]
                {
                    new MockInvalidParameterInfo(InvalidParameterInfo.Kind.AmbiguousParameterValue, "framework", "net")
                }
            };

            yield return new object[]
            {
                new MockNewCommandInput("foo").WithTemplateOption("framework", "net"),
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group").WithChoiceParameter("framework", "netcoreapp2.1"),
                    new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group").WithChoiceParameter("framework", "net5.0"),
                    new MockTemplateInfo("foo", identity: "foo.3", groupIdentity: "foo.group").WithChoiceParameter("framework", "netcoreapp3.1"),
                },
                new MockInvalidParameterInfo[]
                {
                    new MockInvalidParameterInfo(InvalidParameterInfo.Kind.AmbiguousParameterValue, "framework", "net")
                }
            };

            yield return new object[]
            {
                new MockNewCommandInput("foo").WithTemplateOption("framework", "net").WithTemplateOption("fake", "fake").WithTemplateOption("OtherChoice", "fake"),
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group").WithChoiceParameter("framework", "netcoreapp2.1", "netcoreapp3.1").WithChoiceParameter("OtherChoice", "val1", "val2"),
                    new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group").WithChoiceParameter("framework", "net5.0").WithChoiceParameter("OtherChoice", "val1", "val2"),
                },
                new MockInvalidParameterInfo[]
                {
                    new MockInvalidParameterInfo(InvalidParameterInfo.Kind.AmbiguousParameterValue, "framework", "net"),
                    new MockInvalidParameterInfo(InvalidParameterInfo.Kind.InvalidParameterName, "fake", null),
                    new MockInvalidParameterInfo(InvalidParameterInfo.Kind.InvalidParameterValue, "OtherChoice", "fake")
                }
            };
        }

        public static IEnumerable<object[]> GetAmbiguousSingleStartsWithParametersTestData()
        {
            yield return new object[]
            {
                new MockNewCommandInput("foo").WithTemplateOption("framework", "netcoreapp3.1"),
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group").WithChoiceParameter("framework", "netcoreapp2.1", "netcoreapp3.1"),
                    new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group").WithChoiceParameter("framework", "net5.0"),
                },
                System.Array.Empty<MockInvalidParameterInfo>()
            };

            yield return new object[]
            {
                new MockNewCommandInput("foo").WithTemplateOption("framework", "net"),
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group").WithChoiceParameter("framework", "netcoreapp2.1", "netcoreapp3.1"),
                    new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group").WithChoiceParameter("framework", "net5.0"),
                },
                System.Array.Empty<MockInvalidParameterInfo>()
            };

            yield return new object[]
            {
                new MockNewCommandInput("foo").WithTemplateOption("framework", "net"),
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group").WithChoiceParameter("framework", "netcoreapp2.1"),
                    new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group").WithChoiceParameter("framework", "net5.0"),
                },
                new MockInvalidParameterInfo[]
                {
                    new MockInvalidParameterInfo(InvalidParameterInfo.Kind.AmbiguousParameterValue, "framework", "net")
                }
            };

            yield return new object[]
            {
                new MockNewCommandInput("foo").WithTemplateOption("framework", "net").WithTemplateOption("OtherChoice", "val1"),
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group").WithChoiceParameter("framework", "netcoreapp2.1").WithChoiceParameter("OtherChoice", "val1", "val2"),
                    new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group").WithChoiceParameter("framework", "net5.0").WithChoiceParameter("OtherChoice", "val1", "val2"),
                    new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group").WithChoiceParameter("framework", "net").WithChoiceParameter("OtherChoice", "val1", "val2"),
                },
                new MockInvalidParameterInfo[]
                {
                    new MockInvalidParameterInfo(InvalidParameterInfo.Kind.AmbiguousParameterValue, "framework", "net"),
                }
            };
        }

        public static IEnumerable<object[]> GetHighestPrecedenceInvokableTemplatesTestData()
        {
            yield return new object[]
            {
                new MockNewCommandInput("foo"),
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group", precedence: 100),
                    new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group", precedence: 200),
                    new MockTemplateInfo("foo", identity: "foo.3", groupIdentity: "foo.group", precedence: 300),
                },
                null,
                new string[] { "foo.3" }
            };

            yield return new object[]
            {
                new MockNewCommandInput("foo"),
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group", precedence: 300),
                    new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group", precedence: 300),
                    new MockTemplateInfo("foo", identity: "foo.3", groupIdentity: "foo.group", precedence: 300),
                },
                null,
                new string[] { "foo.1", "foo.2", "foo.3" }
            };

            yield return new object[]
            {
                new MockNewCommandInput("foo", language: "VB"),
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group", precedence: 300).WithTag("language", "VB"),
                    new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group", precedence: 300).WithTag("language", "F#"),
                    new MockTemplateInfo("foo", identity: "foo.3", groupIdentity: "foo.group", precedence: 300).WithTag("language", "Lisp"),
                },
                "Lisp",
                new string[] { "foo.1" }
            };

            yield return new object[]
            {
                new MockNewCommandInput("foo"),
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group", precedence: 300).WithTag("language", "VB"),
                    new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group", precedence: 300).WithTag("language", "F#"),
                    new MockTemplateInfo("foo", identity: "foo.3", groupIdentity: "foo.group", precedence: 300).WithTag("language", "Lisp"),
                },
                "Lisp",
                new string[] { "foo.3" }
            };

            yield return new object[]
            {
                new MockNewCommandInput("foo"),
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group", precedence: 300).WithTag("language", "VB"),
                    new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group", precedence: 300).WithTag("language", "F#"),
                    new MockTemplateInfo("foo", identity: "foo.3", groupIdentity: "foo.group", precedence: 300).WithTag("language", "Lisp"),
                },
                null,
                new string[] { "foo.1", "foo.2", "foo.3" }
            };

            yield return new object[]
            {
                new MockNewCommandInput("foo"),
                new MockTemplateInfo[]
                {
                    new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group", precedence: 100).WithTag("language", "VB"),
                    new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group", precedence: 200).WithTag("language", "F#"),
                    new MockTemplateInfo("foo", identity: "foo.3", groupIdentity: "foo.group", precedence: 300).WithTag("language", "Lisp"),
                },
                null,
                new string[] { "foo.3" }
            };
        }

        [Theory(DisplayName = nameof(GetInvalidParametersTest))]
        [MemberData(nameof(GetInvalidParametersTestData))]
        internal void GetInvalidParametersTest(MockNewCommandInput command, MockTemplateInfo[] templates, MockInvalidParameterInfo[] expectedInvalidParams)
        {
            TemplateResolutionResult matchedTemplates = TemplateResolver.GetTemplateResolutionResult(templates, new MockHostSpecificDataLoader(), command, null);

            TemplateGroup templateGroup = matchedTemplates.UnambiguousTemplateGroup;
            IEnumerable<InvalidParameterInfo> invalidParameters = templateGroup.GetInvalidParameterList();

            Assert.Equal(expectedInvalidParams.Length, invalidParameters.Count());
            foreach (MockInvalidParameterInfo exp in expectedInvalidParams)
            {
                Assert.Single(invalidParameters.Where(param => param.ErrorKind == exp.Kind && param.InputFormat == exp.InputFormat && param.SpecifiedValue == exp.SpecifiedValue));
            }
        }

        [Theory(DisplayName = nameof(GetAmbiguousSingleStartsWithParametersTest))]
        [MemberData(nameof(GetAmbiguousSingleStartsWithParametersTestData))]
        internal void GetAmbiguousSingleStartsWithParametersTest(MockNewCommandInput command, MockTemplateInfo[] templates, MockInvalidParameterInfo[] expectedInvalidParams)
        {
            TemplateResolutionResult matchedTemplates = TemplateResolver.GetTemplateResolutionResult(templates, new MockHostSpecificDataLoader(), command, null);

            TemplateGroup templateGroup = matchedTemplates.UnambiguousTemplateGroup;
            IEnumerable<InvalidParameterInfo> invalidParameters = templateGroup.GetAmbiguousSingleStartsWithParameters();

            Assert.Equal(expectedInvalidParams.Length, invalidParameters.Count());
            foreach (MockInvalidParameterInfo exp in expectedInvalidParams)
            {
                Assert.Single(invalidParameters.Where(param => param.ErrorKind == exp.Kind && param.InputFormat == exp.InputFormat && param.SpecifiedValue == exp.SpecifiedValue));
            }
        }

        [Theory(DisplayName = nameof(GetHighestPrecedenceInvokableTemplatesTest))]
        [MemberData(nameof(GetHighestPrecedenceInvokableTemplatesTestData))]
        internal void GetHighestPrecedenceInvokableTemplatesTest(MockNewCommandInput command, MockTemplateInfo[] templates, string defaultLanguage, string[] expectedTemplates)
        {
            TemplateResolutionResult matchedTemplates = TemplateResolver.GetTemplateResolutionResult(templates, new MockHostSpecificDataLoader(), command, defaultLanguage);

            TemplateGroup templateGroup = matchedTemplates.UnambiguousTemplateGroup;
            bool useDefaultLanguage = string.IsNullOrWhiteSpace(command.Language) && !string.IsNullOrWhiteSpace(defaultLanguage);
            IEnumerable<ITemplateMatchInfo> selectedTemplates = templateGroup.GetHighestPrecedenceInvokableTemplates(useDefaultLanguage);

            var identitiesToCheck = selectedTemplates.Select(t => t.Info.Identity);

            Assert.Equal(expectedTemplates.Length, selectedTemplates.Count());
            foreach (string exp in expectedTemplates)
            {
                Assert.Single(selectedTemplates.Where(t => t.Info.Identity == exp));
            }

            bool success = templateGroup.TryGetHighestPrecedenceInvokableTemplate(out ITemplateMatchInfo selectedTemplate, useDefaultLanguage);

            if (expectedTemplates.Length == 1)
            {
                Assert.Equal(expectedTemplates[0], selectedTemplate.Info.Identity);
                Assert.True(success);
            }
            else
            {
                Assert.Null(selectedTemplate);
                Assert.False(success);
            }
        }
    }
}
