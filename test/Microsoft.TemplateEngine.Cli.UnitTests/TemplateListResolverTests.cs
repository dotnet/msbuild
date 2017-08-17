using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Cli.UnitTests.CliMocks;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Utils;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    // Implementation notes:
    // If a test is going to hit the secondary matching in the resolver, make sure to initialize the Tags & CacheParameters,
    //  otherwise an exception will be thrown in TemplateInfo.Parameters getter
    // MockNewCommandInput doesn't support everything in the interface, just enough for this type of testing.
    public class TemplateListResolverTests
    {
        [Fact(DisplayName = nameof(TestFindHighestPrecedenceTemplateIfAllSameGroupIdentity))]
        public void TestFindHighestPrecedenceTemplateIfAllSameGroupIdentity()
        {
            List<IFilteredTemplateInfo> templatesToCheck = new List<IFilteredTemplateInfo>();
            templatesToCheck.Add(new FilteredTemplateInfo(
                new TemplateInfo()
                {
                    Precedence = 10,
                    Name = "Template1",
                    Identity = "Template1",
                    GroupIdentity = "TestGroup"
                }
                , null));
            templatesToCheck.Add(new FilteredTemplateInfo(
                new TemplateInfo()
                {
                    Precedence = 20,
                    Name = "Template2",
                    Identity = "Template2",
                    GroupIdentity = "TestGroup"
                }
                , null));
            templatesToCheck.Add(new FilteredTemplateInfo(
                new TemplateInfo()
                {
                    Precedence = 0,
                    Name = "Template3",
                    Identity = "Template3",
                    GroupIdentity = "TestGroup"
                }
                , null));

            IFilteredTemplateInfo highestPrecedenceTemplate = TemplateListResolver.FindHighestPrecedenceTemplateIfAllSameGroupIdentity(templatesToCheck);
            Assert.NotNull(highestPrecedenceTemplate);
            Assert.Equal("Template2", highestPrecedenceTemplate.Info.Identity);
            Assert.Equal(20, highestPrecedenceTemplate.Info.Precedence);
        }

        [Fact(DisplayName = nameof(TestFindHighestPrecedenceTemplateIfAllSameGroupIdentity_ReturnsNullIfGroupsAreDifferent))]
        public void TestFindHighestPrecedenceTemplateIfAllSameGroupIdentity_ReturnsNullIfGroupsAreDifferent()
        {
            List<IFilteredTemplateInfo> templatesToCheck = new List<IFilteredTemplateInfo>();
            templatesToCheck.Add(new FilteredTemplateInfo(
                new TemplateInfo()
                {
                    Precedence = 10,
                    Name = "Template1",
                    Identity = "Template1",
                    GroupIdentity = "TestGroup"
                }
                , null));
            templatesToCheck.Add(new FilteredTemplateInfo(
                new TemplateInfo()
                {
                    Precedence = 20,
                    Name = "Template2",
                    Identity = "Template2",
                    GroupIdentity = "RealGroup"
                }
                , null));
            IFilteredTemplateInfo highestPrecedenceTemplate = TemplateListResolver.FindHighestPrecedenceTemplateIfAllSameGroupIdentity(templatesToCheck);
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
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "type", CreateTestCacheTag("project") }
                }
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                Name = "Template2",
                Identity = "Template2",
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "type", CreateTestCacheTag("item") }
                }
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                Name = "Template3",
                Identity = "Template3",
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "type", CreateTestCacheTag("myType") }
                }
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                Name = "Template4",
                Identity = "Template4",
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "type", CreateTestCacheTag("project") }
                }
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                Name = "Template5",
                Identity = "Template5",
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "type", CreateTestCacheTag("project") }
                }
            });

            IHostSpecificDataLoader hostDataLoader = new MockHostSpecificDataLoader();

            IReadOnlyCollection<IFilteredTemplateInfo> projectTemplates = TemplateListResolver.PerformAllTemplatesInContextQuery(templatesToSearch, hostDataLoader, "project");
            Assert.Equal(3, projectTemplates.Count);
            Assert.True(projectTemplates.Where(x => string.Equals(x.Info.Identity, "Template1", StringComparison.Ordinal)).Any());
            Assert.True(projectTemplates.Where(x => string.Equals(x.Info.Identity, "Template4", StringComparison.Ordinal)).Any());
            Assert.True(projectTemplates.Where(x => string.Equals(x.Info.Identity, "Template5", StringComparison.Ordinal)).Any());

            IReadOnlyCollection<IFilteredTemplateInfo> itemTemplates = TemplateListResolver.PerformAllTemplatesInContextQuery(templatesToSearch, hostDataLoader, "item");
            Assert.Equal(1, itemTemplates.Count);
            Assert.True(itemTemplates.Where(x => string.Equals(x.Info.Identity, "Template2", StringComparison.Ordinal)).Any());

            IReadOnlyCollection<IFilteredTemplateInfo> otherTemplates = TemplateListResolver.PerformAllTemplatesInContextQuery(templatesToSearch, hostDataLoader, "other");
            Assert.Equal(1, otherTemplates.Count);
            Assert.True(otherTemplates.Where(x => string.Equals(x.Info.Identity, "Template3", StringComparison.Ordinal)).Any());
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
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "Template2",
                Name = "Long name of Template2",
                Identity = "Template2",
            });

            INewCommandInput userInputs = new MockNewCommandInput()
            {
                TemplateName = "Template2"
            };

            TemplateListResolutionResult matchResult = TemplateListResolver.PerformCoreTemplateQuery(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.Equal(null, matchResult.MatchedTemplatesWithSecondaryMatchInfo);
            Assert.Equal(1, matchResult.CoreMatchedTemplates.Count);
            Assert.Equal(1, matchResult.UnambiguousTemplateGroupToUse.Count);
            Assert.Equal("Template2", matchResult.CoreMatchedTemplates[0].Info.Identity);
            Assert.Equal("Template2", matchResult.UnambiguousTemplateGroupToUse[0].Info.Identity);
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
                    { "language", CreateTestCacheTag("Perl") }
                }
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "foo",
                Name = "Description of foo LISP template",
                Identity = "foo.test.Lisp",
                GroupIdentity = "foo.test.template",
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", CreateTestCacheTag("LISP") }
                }
            });

            INewCommandInput userInputs = new MockNewCommandInput()
            {
                TemplateName = "foo"
            };

            TemplateListResolutionResult matchResult = TemplateListResolver.PerformCoreTemplateQuery(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, "Perl");
            Assert.Equal(null, matchResult.MatchedTemplatesWithSecondaryMatchInfo);
            Assert.Equal(2, matchResult.CoreMatchedTemplates.Count);    // they both match the initial query. Default language is secondary
            Assert.Equal(1, matchResult.UnambiguousTemplateGroupToUse.Count);
            Assert.Equal("foo.test.Perl", matchResult.UnambiguousTemplateGroupToUse[0].Info.Identity);
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
                    { "language", CreateTestCacheTag("Perl") }
                }
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "foo",
                Name = "Description of foo LISP template",
                Identity = "foo.test.Lisp",
                GroupIdentity = "foo.test.template",
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", CreateTestCacheTag("LISP") }
                }
            });

            INewCommandInput userInputs = new MockNewCommandInput()
            {
                TemplateName = "foo",
                Language = "LISP"
            };

            TemplateListResolutionResult matchResult = TemplateListResolver.PerformCoreTemplateQuery(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, "Perl");
            Assert.Equal(null, matchResult.MatchedTemplatesWithSecondaryMatchInfo);
            Assert.Equal(1, matchResult.CoreMatchedTemplates.Count);    // Input language is part of the initial checks.
            Assert.Equal(1, matchResult.UnambiguousTemplateGroupToUse.Count);
            Assert.Equal("foo.test.Lisp", matchResult.UnambiguousTemplateGroupToUse[0].Info.Identity);
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
                Precedence = 100
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "foo",
                Name = "Foo template new",
                Identity = "foo.test.new",
                GroupIdentity = "foo.test.template",
                Precedence = 200
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "bar",
                Name = "Bar template",
                Identity = "bar.test",
                GroupIdentity = "bar.test.template",
                Precedence = 100
            });

            INewCommandInput userInputs = new MockNewCommandInput()
            {
                TemplateName = "foo"
            };

            TemplateListResolutionResult matchResult = TemplateListResolver.PerformCoreTemplateQuery(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.Equal(2, matchResult.MatchedTemplatesWithSecondaryMatchInfo.Count);
            Assert.Equal(2, matchResult.CoreMatchedTemplates.Count);
            Assert.Equal(2, matchResult.UnambiguousTemplateGroupToUse.Count);
            Assert.True(matchResult.UnambiguousTemplateGroupToUse.Any(x => string.Equals(x.Info.Identity, "foo.test.old")));
            Assert.True(matchResult.UnambiguousTemplateGroupToUse.Any(x => string.Equals(x.Info.Identity, "foo.test.new")));
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

            INewCommandInput userInputs = new MockNewCommandInput(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "baz", "whatever" }
                }
            )
            {
                TemplateName = "foo"
            };

            TemplateListResolutionResult matchResult = TemplateListResolver.PerformCoreTemplateQuery(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.Equal(2, matchResult.MatchedTemplatesWithSecondaryMatchInfo.Count);
            Assert.Equal(1, matchResult.UnambiguousTemplateGroupToUse.Count);
            Assert.Equal("foo.test.new", matchResult.UnambiguousTemplateGroupToUse[0].Info.Identity);
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
                    { "framework", CreateTestCacheTag(new List<string>() { "netcoreapp1.0", "netcoreapp1.1" }) }
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
                    { "framework", CreateTestCacheTag(new List<string>() { "netcoreapp2.0" }) }
                },
                CacheParameters = new Dictionary<string, ICacheParameter>()
            });

            INewCommandInput userInputs = new MockNewCommandInput(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "framework", "netcoreapp1.0" }
                }
            )
            {
                TemplateName = "foo"
            };

            TemplateListResolutionResult matchResult = TemplateListResolver.PerformCoreTemplateQuery(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.Equal(2, matchResult.MatchedTemplatesWithSecondaryMatchInfo.Count);
            Assert.Equal(1, matchResult.UnambiguousTemplateGroupToUse.Count);
            Assert.Equal("foo.test.old", matchResult.UnambiguousTemplateGroupToUse[0].Info.Identity);
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

            INewCommandInput userInputs = new MockNewCommandInput(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "baz", null }
                }
            )
            {
                TemplateName = "foo"
            };

            IHostSpecificDataLoader hostSpecificDataLoader = new MockHostSpecificDataLoader();

            TemplateListResolutionResult matchResult = TemplateListResolver.PerformCoreTemplateQuery(templatesToSearch, hostSpecificDataLoader, userInputs, null);
            Assert.Equal(null, matchResult.MatchedTemplatesWithSecondaryMatchInfo);
            Assert.Equal(1, matchResult.UnambiguousTemplateGroupToUse.Count);

            // TODO:
            // These 2 lines are the analogues of what happens in New3Command.EnterSingularTemplateManipulationFlowAsync()
            //      as a final verification for the template.
            // This is really a shortcoming of the resolver, this check should get factored into it.
            // But we'll need to add a new match category so we know that the unambiguous template "matched", except the extra user parameters.
            HostSpecificTemplateData hostTemplateData = hostSpecificDataLoader.ReadHostSpecificTemplateData(matchResult.UnambiguousTemplateGroupToUse[0].Info);
            userInputs.ReparseForTemplate(matchResult.UnambiguousTemplateGroupToUse[0].Info, hostTemplateData);

            Assert.False(TemplateListResolver.ValidateRemainingParameters(userInputs, out IReadOnlyList<string> invalidParams));
            Assert.Equal(1, invalidParams.Count);
            Assert.Equal("baz", invalidParams[0]);
        }

        [Fact(DisplayName = nameof(TestPerformCoreTempalteQuery_InvalidChoiceValueInvalidatesMatch))]
        public void TestPerformCoreTempalteQuery_InvalidChoiceValueInvalidatesMatch()
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
                    { "framework", CreateTestCacheTag(new List<string>() { "netcoreapp1.0", "netcoreapp1.1" }) }
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
                    { "framework", CreateTestCacheTag(new List<string>() { "netcoreapp2.0" }) }
                },
                CacheParameters = new Dictionary<string, ICacheParameter>()
            });

            INewCommandInput userInputs = new MockNewCommandInput(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "framework", "netcoreapp3.0" }
                }
            )
            {
                TemplateName = "foo"
            };

            IHostSpecificDataLoader hostSpecificDataLoader = new MockHostSpecificDataLoader();

            TemplateListResolutionResult matchResult = TemplateListResolver.PerformCoreTemplateQuery(templatesToSearch, hostSpecificDataLoader, userInputs, null);
            Assert.Equal(2, matchResult.MatchedTemplatesWithSecondaryMatchInfo.Count);
            Assert.Equal(2, matchResult.UnambiguousTemplateGroupToUse.Count);
            Assert.Equal(2, matchResult.CoreMatchedTemplates.Count);

            Assert.True(matchResult.UnambiguousTemplateGroupToUse[0].MatchDisposition.Any(x => x.Kind == MatchKind.InvalidParameterValue));
            Assert.True(matchResult.UnambiguousTemplateGroupToUse[1].MatchDisposition.Any(x => x.Kind == MatchKind.InvalidParameterValue));
        }

        private static ICacheTag CreateTestCacheTag(string choice, string description = null, string defaultValue = null)
        {
            return new CacheTag(null,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { choice, description }
                },
                defaultValue);
        }

        private static ICacheTag CreateTestCacheTag(IReadOnlyList<string> choiceList, string description = null, string defaultValue = null)
        {
            Dictionary<string, string> choicesDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach(string choice in choiceList)
            {
                choicesDict.Add(choice, null);
            };

            return new CacheTag(null, choicesDict, defaultValue);
        }
    }
}
