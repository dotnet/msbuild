// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Cli.UnitTests.CliMocks;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Utils;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests.TemplateResolutionTests
{
    // Implementation notes:
    // If a test is going to hit the secondary matching in the resolver, make sure to initialize the Tags & CacheParameters,
    //  otherwise an exception will be thrown in TemplateInfo.Parameters getter
    //  (just about every situation will get to the secondary matching)
    // MockNewCommandInput doesn't support everything in the interface, just enough for this type of testing.
    public class TemplateResolverTests
    {
        [Fact(DisplayName = nameof(TestFindHighestPrecedenceTemplateIfAllSameGroupIdentity))]
        public void TestFindHighestPrecedenceTemplateIfAllSameGroupIdentity()
        {
            List<ITemplateMatchInfo> templatesToCheck = new List<ITemplateMatchInfo>();
            templatesToCheck.Add(new TemplateMatchInfo(
                new TemplateInfo()
                {
                    Precedence = 10,
                    Name = "Template1",
                    Identity = "Template1",
                    GroupIdentity = "TestGroup"
                }
                , null));
            templatesToCheck.Add(new TemplateMatchInfo(
                new TemplateInfo()
                {
                    Precedence = 20,
                    Name = "Template2",
                    Identity = "Template2",
                    GroupIdentity = "TestGroup"
                }
                , null));
            templatesToCheck.Add(new TemplateMatchInfo(
                new TemplateInfo()
                {
                    Precedence = 0,
                    Name = "Template3",
                    Identity = "Template3",
                    GroupIdentity = "TestGroup"
                }));

            ITemplateMatchInfo highestPrecedenceTemplate = TemplateResolver.FindHighestPrecedenceTemplateIfAllSameGroupIdentity(templatesToCheck);
            Assert.NotNull(highestPrecedenceTemplate);
            Assert.Equal("Template2", highestPrecedenceTemplate.Info.Identity);
            Assert.Equal(20, highestPrecedenceTemplate.Info.Precedence);
        }

        [Fact(DisplayName = nameof(TestFindHighestPrecedenceTemplateIfAllSameGroupIdentity_ReturnsNullIfGroupsAreDifferent))]
        public void TestFindHighestPrecedenceTemplateIfAllSameGroupIdentity_ReturnsNullIfGroupsAreDifferent()
        {
            List<ITemplateMatchInfo> templatesToCheck = new List<ITemplateMatchInfo>();
            templatesToCheck.Add(new TemplateMatchInfo(
                new TemplateInfo()
                {
                    Precedence = 10,
                    Name = "Template1",
                    Identity = "Template1",
                    GroupIdentity = "TestGroup"
                }
                , null));
            templatesToCheck.Add(new TemplateMatchInfo(
                new TemplateInfo()
                {
                    Precedence = 20,
                    Name = "Template2",
                    Identity = "Template2",
                    GroupIdentity = "RealGroup"
                }
                , null));
            ITemplateMatchInfo highestPrecedenceTemplate = TemplateResolver.FindHighestPrecedenceTemplateIfAllSameGroupIdentity(templatesToCheck);
            Assert.Null(highestPrecedenceTemplate);
        }

        [Fact(DisplayName = nameof(TestPerformAllTemplatesInContextQuery))]
        public void TestPerformAllTemplatesInContextQuery()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new TemplateInfo()
            {
                Name = "Template1",
                Identity = "Template1",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "type", ResolutionTestHelper.CreateTestCacheTag("project") }
                },
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                Name = "Template2",
                Identity = "Template2",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "type", ResolutionTestHelper.CreateTestCacheTag("item") }
                }
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                Name = "Template3",
                Identity = "Template3",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "type", ResolutionTestHelper.CreateTestCacheTag("myType") }
                }
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                Name = "Template4",
                Identity = "Template4",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "type",ResolutionTestHelper.CreateTestCacheTag("project") }
                }
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                Name = "Template5",
                Identity = "Template5",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "type", ResolutionTestHelper.CreateTestCacheTag("project") }
                }
            });

            IHostSpecificDataLoader hostDataLoader = new MockHostSpecificDataLoader();

            IReadOnlyCollection<ITemplateMatchInfo> projectTemplates = TemplateResolver.PerformAllTemplatesInContextQuery(templatesToSearch, hostDataLoader, "project");
            Assert.Equal(3, projectTemplates.Count);
            Assert.True(projectTemplates.Where(x => string.Equals(x.Info.Identity, "Template1", StringComparison.Ordinal)).Any());
            Assert.True(projectTemplates.Where(x => string.Equals(x.Info.Identity, "Template4", StringComparison.Ordinal)).Any());
            Assert.True(projectTemplates.Where(x => string.Equals(x.Info.Identity, "Template5", StringComparison.Ordinal)).Any());

            IReadOnlyCollection<ITemplateMatchInfo> itemTemplates = TemplateResolver.PerformAllTemplatesInContextQuery(templatesToSearch, hostDataLoader, "item");
            Assert.Equal(1, itemTemplates.Count);
            Assert.True(itemTemplates.Where(x => string.Equals(x.Info.Identity, "Template2", StringComparison.Ordinal)).Any());

            //Visual Studio only supports "project" and "item", so using other types is no longer allowed, therefore "other" handling is removed.
            //support of match on custom type still remains
            IReadOnlyCollection<ITemplateMatchInfo> otherTemplates = TemplateResolver.PerformAllTemplatesInContextQuery(templatesToSearch, hostDataLoader, "other");
            Assert.Equal(0, otherTemplates.Count);
            Assert.False(otherTemplates.Where(x => string.Equals(x.Info.Identity, "Template3", StringComparison.Ordinal)).Any());

            IReadOnlyCollection<ITemplateMatchInfo> customTypeTemplates = TemplateResolver.PerformAllTemplatesInContextQuery(templatesToSearch, hostDataLoader, "myType");
            Assert.Equal(1, customTypeTemplates.Count);
            Assert.True(customTypeTemplates.Where(x => string.Equals(x.Info.Identity, "Template3", StringComparison.Ordinal)).Any());
        }

        [Fact(DisplayName = nameof(TestPerformCoreTemplateQuery_UniqueNameMatchesCorrectly))]
        public void TestPerformCoreTemplateQuery_UniqueNameMatchesCorrectly()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "Template1",
                Name = "Long name of Template1",
                Identity = "Template1",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>()
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "Template2",
                Name = "Long name of Template2",
                Identity = "Template2",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>()
            });

            INewCommandInput userInputs = new MockNewCommandInput("Template2");

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);

            Assert.Equal(1, matchResult.GetBestTemplateMatchList().Count);
            Assert.Equal("Template2", matchResult.GetBestTemplateMatchList()[0].Info.Identity);
            Assert.True(matchResult.TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousGroup));
            Assert.Equal(1, unambiguousGroup.Count);
            Assert.Equal("Template2", unambiguousGroup[0].Info.Identity);
        }

        [Fact(DisplayName = nameof(TestPerformCoreTemplateQuery_DefaultLanguageDisambiguates))]
        public void TestPerformCoreTemplateQuery_DefaultLanguageDisambiguates()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "foo",
                Name = "Description of foo Perl template",
                Identity = "foo.test.Perl",
                GroupIdentity = "foo.test.template",
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("Perl") }
                },
                CacheParameters = new Dictionary<string, ICacheParameter>()
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "foo",
                Name = "Description of foo LISP template",
                Identity = "foo.test.Lisp",
                GroupIdentity = "foo.test.template",
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("LISP") }
                },
                CacheParameters = new Dictionary<string, ICacheParameter>()
            });

            INewCommandInput userInputs = new MockNewCommandInput("foo");

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, "Perl");
            Assert.Equal(1, matchResult.GetBestTemplateMatchList().Count);
            Assert.True(matchResult.TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousGroup));
            Assert.Equal(1, unambiguousGroup.Count);
            Assert.Equal("foo.test.Perl", unambiguousGroup[0].Info.Identity);
        }

        [Fact(DisplayName = nameof(TestPerformCoreTemplateQuery_InputLanguageIsPreferredOverDefault))]
        public void TestPerformCoreTemplateQuery_InputLanguageIsPreferredOverDefault()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "foo",
                Name = "Description of foo Perl template",
                Identity = "foo.test.Perl",
                GroupIdentity = "foo.test.template",
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("Perl") }
                },
                CacheParameters = new Dictionary<string, ICacheParameter>()
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "foo",
                Name = "Description of foo LISP template",
                Identity = "foo.test.Lisp",
                GroupIdentity = "foo.test.template",
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("LISP") }
                },
                CacheParameters = new Dictionary<string, ICacheParameter>()
            });

            INewCommandInput userInputs = new MockNewCommandInput("foo", "LISP");

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, "Perl");

            Assert.Equal(1, matchResult.GetBestTemplateMatchList().Count);
            Assert.True(matchResult.TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousGroup));
            Assert.Equal(1, unambiguousGroup.Count);
            Assert.Equal("foo.test.Lisp", unambiguousGroup[0].Info.Identity);
        }

        [Fact(DisplayName = nameof(TestPerformCoreTemplateQuery_GroupIsFound))]
        public void TestPerformCoreTemplateQuery_GroupIsFound()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "foo",
                Name = "Foo template old",
                Identity = "foo.test.old",
                GroupIdentity = "foo.test.template",
                Precedence = 100,
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>()
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "foo",
                Name = "Foo template new",
                Identity = "foo.test.new",
                GroupIdentity = "foo.test.template",
                Precedence = 200,
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>()
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "bar",
                Name = "Bar template",
                Identity = "bar.test",
                GroupIdentity = "bar.test.template",
                Precedence = 100,
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>()
            });

            INewCommandInput userInputs = new MockNewCommandInput("foo");

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.Equal(2, matchResult.GetBestTemplateMatchList().Count);
            Assert.True(matchResult.TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousGroup));
            Assert.Equal(2, unambiguousGroup.Count);
            Assert.Contains(unambiguousGroup, x => string.Equals(x.Info.Identity, "foo.test.old"));
            Assert.Contains(unambiguousGroup, x => string.Equals(x.Info.Identity, "foo.test.new"));
        }

        [Fact(DisplayName = nameof(TestPerformCoreTemplateQuery_ParameterNameDisambiguates))]
        public void TestPerformCoreTemplateQuery_ParameterNameDisambiguates()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "foo",
                Name = "Foo template",
                Identity = "foo.test.old",
                GroupIdentity = "foo.test.template",
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase),
                CacheParameters = new Dictionary<string, ICacheParameter>(StringComparer.OrdinalIgnoreCase)
                {
                    { "bar", new CacheParameter() },
                }
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "foo",
                Name = "Foo template",
                Identity = "foo.test.new",
                GroupIdentity = "foo.test.template",
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase),
                CacheParameters = new Dictionary<string, ICacheParameter>(StringComparer.OrdinalIgnoreCase)
                {
                    { "baz", new CacheParameter() },
                }
            });

            INewCommandInput userInputs = new MockNewCommandInput("foo").WithTemplateOption("baz", "whatever");

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.True(matchResult.TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousGroup));
            Assert.Equal(1, unambiguousGroup.Count);
            Assert.Equal("foo.test.new", unambiguousGroup[0].Info.Identity);
        }

        [Fact(DisplayName = nameof(TestPerformCoreTemplateQuery_ParameterValueDisambiguates))]
        public void TestPerformCoreTemplateQuery_ParameterValueDisambiguates()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "foo",
                Name = "Foo template old",
                Identity = "foo.test.old",
                GroupIdentity = "foo.test.template",
                Precedence = 100,
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "framework", ResolutionTestHelper.CreateTestCacheTag(new List<string>() { "netcoreapp1.0", "netcoreapp1.1" }) }
                },
                CacheParameters = new Dictionary<string, ICacheParameter>()
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "foo",
                Name = "Foo template new",
                Identity = "foo.test.new",
                GroupIdentity = "foo.test.template",
                Precedence = 200,
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "framework", ResolutionTestHelper.CreateTestCacheTag(new List<string>() { "netcoreapp2.0" }) }
                },
                CacheParameters = new Dictionary<string, ICacheParameter>()
            });

            INewCommandInput userInputs = new MockNewCommandInput("foo").WithTemplateOption("framework", "netcoreapp1.0");

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.True(matchResult.TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousGroup));
            Assert.Equal(1, unambiguousGroup.Count);
            Assert.Equal("foo.test.old", unambiguousGroup[0].Info.Identity);
        }

        [Fact(DisplayName = nameof(TestPerformCoreTemplateQuery_UnknownParameterNameInvalidatesMatch))]
        public void TestPerformCoreTemplateQuery_UnknownParameterNameInvalidatesMatch()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "foo",
                Name = "Foo template",
                Identity = "foo.test",
                GroupIdentity = "foo.test.template",
                Precedence = 100,
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase),
                CacheParameters = new Dictionary<string, ICacheParameter>()
                {
                    { "bar", new CacheParameter() },
                }
            });

            INewCommandInput userInputs = new MockNewCommandInput("foo").WithTemplateOption("baz");

            IHostSpecificDataLoader hostSpecificDataLoader = new MockHostSpecificDataLoader();

            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(templatesToSearch, hostSpecificDataLoader, userInputs, null);
            Assert.True(matchResult.TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousGroup));
            Assert.Equal(1, unambiguousGroup.Count);

            Assert.False(TemplateResolver.ValidateRemainingParameters(unambiguousGroup[0], out IReadOnlyList<string> invalidParams));
            Assert.Equal(1, invalidParams.Count);
            Assert.Equal("baz", invalidParams[0]);
        }

        [Fact(DisplayName = nameof(TestPerformCoreTemplateQuery_InvalidChoiceValueInvalidatesMatch))]
        public void TestPerformCoreTemplateQuery_InvalidChoiceValueInvalidatesMatch()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "foo",
                Name = "Foo template",
                Identity = "foo.test.1x",
                GroupIdentity = "foo.test.template",
                Precedence = 100,
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "framework", ResolutionTestHelper.CreateTestCacheTag(new List<string>() { "netcoreapp1.0", "netcoreapp1.1" }) }
                },
                CacheParameters = new Dictionary<string, ICacheParameter>()
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "foo",
                Name = "Foo template",
                Identity = "foo.test.2x",
                GroupIdentity = "foo.test.template",
                Precedence = 200,
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "framework", ResolutionTestHelper.CreateTestCacheTag(new List<string>() { "netcoreapp2.0" }) }
                },
                CacheParameters = new Dictionary<string, ICacheParameter>()
            });

            INewCommandInput userInputs = new MockNewCommandInput("foo").WithTemplateOption("framework", "netcoreapp3.0");


            IHostSpecificDataLoader hostSpecificDataLoader = new MockHostSpecificDataLoader();
            TemplateResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResult(templatesToSearch, hostSpecificDataLoader, userInputs, null);
            Assert.True(matchResult.TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousGroup));
            Assert.Equal(2, unambiguousGroup.Count);
            Assert.Equal(2, matchResult.GetBestTemplateMatchList().Count);

            Assert.Contains(unambiguousGroup[0].MatchDisposition, x => x.Kind == MatchKind.InvalidParameterValue);
            Assert.Contains(unambiguousGroup[1].MatchDisposition, x => x.Kind == MatchKind.InvalidParameterValue);
        }
    }
}
