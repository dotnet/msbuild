// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Cli.UnitTests.CliMocks;
using System.Linq;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests.TemplateResolutionTests
{
    public class ListTemplateListResolverTests
    {
        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_UniqueNameMatchesCorrectly))]
        public void TestGetTemplateResolutionResult_UniqueNameMatchesCorrectly()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console1",
                Name = "Long name for Console App",
                Identity = "Console.App",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>()
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console2",
                Name = "Long name for Console App #2",
                Identity = "Console.App2",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>()
            });

            INewCommandInput userInputs = new MockNewCommandInput()
            {
                TemplateName = "console2",
                IsListFlagSpecified = true
            };

            ListOrHelpTemplateListResolutionResult matchResult = TemplateListResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.True(matchResult.HasExactMatches);
            Assert.True(matchResult.HasUnambiguousTemplateGroup);
            Assert.Equal("console2", matchResult.UnambiguousTemplateGroup.Single().Info.ShortName);
            Assert.Equal("Console.App2", matchResult.UnambiguousTemplateGroup.Single().Info.Identity);
            Assert.Equal(1, matchResult.UnambiguousTemplateGroup.Count);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_ExactMatchOnShortNameMatchesCorrectly))]
        public void TestGetTemplateResolutionResult_ExactMatchOnShortNameMatchesCorrectly()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App",
                Identity = "Console.App",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>()
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console2",
                Name = "Long name for Console App #2",
                Identity = "Console.App2",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>()
            });

            INewCommandInput userInputs = new MockNewCommandInput()
            {
                TemplateName = "console",
                IsListFlagSpecified = true
            };

            ListOrHelpTemplateListResolutionResult matchResult = TemplateListResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.True(matchResult.HasExactMatches);
            Assert.False(matchResult.HasUnambiguousTemplateGroup);
            Assert.Equal(2, matchResult.ExactMatchedTemplatesGrouped.Count);
            Assert.Equal(2, matchResult.ExactMatchedTemplates.Count);
            Assert.NotNull(matchResult.ExactMatchedTemplates.Single(t => t.Info.Identity == "Console.App"));
            Assert.NotNull(matchResult.ExactMatchedTemplates.Single(t => t.Info.Identity == "Console.App2"));
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_UnambiguousGroupIsFound))]
        public void TestGetTemplateResolutionResult_UnambiguousGroupIsFound()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App",
                Identity = "Console.App.L1",
                GroupIdentity = "Console.App.Test",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L1") }
                }
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App",
                Identity = "Console.App.L2",
                GroupIdentity = "Console.App.Test",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L2") }
                }
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App",
                Identity = "Console.App.L3",
                GroupIdentity = "Console.App.Test",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L3") }
                }
            });

            INewCommandInput userInputs = new MockNewCommandInput()
            {
                TemplateName = "console",
                IsListFlagSpecified = true
            };

            ListOrHelpTemplateListResolutionResult matchResult = TemplateListResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.True(matchResult.HasExactMatches);
            Assert.Equal(1, matchResult.ExactMatchedTemplatesGrouped.Count);
            Assert.Equal(3, matchResult.ExactMatchedTemplates.Count);
            Assert.True(matchResult.HasUnambiguousTemplateGroup);
            Assert.Equal(3, matchResult.UnambiguousTemplateGroup.Count);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_MultipleGroupsAreFound))]
        public void TestGetTemplateResolutionResult_MultipleGroupsAreFound()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App",
                Identity = "Console.App.L1",
                GroupIdentity = "Console.App.Test",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L1") }
                }
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App",
                Identity = "Console.App.L2",
                GroupIdentity = "Console.App.Test",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L2") }
                }
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App",
                Identity = "Console.App.L3",
                GroupIdentity = "Console.App.Test",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L3") }
                }
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "classlib",
                Name = "Long name for Class Library App",
                Identity = "Class.Library.L1",
                GroupIdentity = "Class.Library.Test",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L1") }
                }
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "classlib",
                Name = "Long name for Class Library App",
                Identity = "Class.Library.L2",
                GroupIdentity = "Class.Library.Test",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L2") }
                }
            });

            INewCommandInput userInputs = new MockNewCommandInput()
            {
                TemplateName = "c",
                IsListFlagSpecified = true
            };

            ListOrHelpTemplateListResolutionResult matchResult = TemplateListResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.True(matchResult.HasExactMatches);
            Assert.Equal(2, matchResult.ExactMatchedTemplatesGrouped.Count);
            Assert.Equal(5, matchResult.ExactMatchedTemplates.Count);
            Assert.False(matchResult.HasUnambiguousTemplateGroup);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_DefaultLanguageDisambiguates))]
        public void TestGetTemplateResolutionResult_DefaultLanguageDisambiguates()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App",
                Identity = "Console.App.L1",
                GroupIdentity = "Console.App.Test",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L1") }
                }
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App",
                Identity = "Console.App.L2",
                GroupIdentity = "Console.App.Test",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L2") }
                }
            });

            INewCommandInput userInputs = new MockNewCommandInput()
            {
                TemplateName = "console",
                IsListFlagSpecified = true
            };

            ListOrHelpTemplateListResolutionResult matchResult = TemplateListResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, "L1");
            Assert.True(matchResult.HasExactMatches);
            Assert.True(matchResult.HasUnambiguousTemplateGroup);
            Assert.Equal(1, matchResult.ExactMatchedTemplatesGrouped.Count);
            Assert.Equal(2, matchResult.ExactMatchedTemplates.Count);
            Assert.NotNull(matchResult.ExactMatchedTemplates.Single(t => t.Info.Identity == "Console.App.L1"));
            Assert.NotNull(matchResult.ExactMatchedTemplates.Single(t => t.Info.Identity == "Console.App.L2"));
            Assert.False(matchResult.HasUnambiguousTemplateGroupForDefaultLanguage);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_InputLanguageIsPreferredOverDefault))]
        public void TestGetTemplateResolutionResult_InputLanguageIsPreferredOverDefault()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App",
                Identity = "Console.App.L1",
                GroupIdentity = "Console.App.Test",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L1") }
                }
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App",
                Identity = "Console.App.L2",
                GroupIdentity = "Console.App.Test",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L2") }
                }
            });

            INewCommandInput userInputs = new MockNewCommandInput()
            {
                TemplateName = "console",
                IsListFlagSpecified = true,
                Language = "L2"
            };

            ListOrHelpTemplateListResolutionResult matchResult = TemplateListResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, "L1");
            Assert.True(matchResult.HasExactMatches);
            Assert.True(matchResult.HasUnambiguousTemplateGroup);
            Assert.Equal("console", matchResult.UnambiguousTemplateGroup.Single().Info.ShortName);
            Assert.Equal("Console.App.L2", matchResult.UnambiguousTemplateGroup.Single().Info.Identity);
            Assert.Equal("L2", matchResult.UnambiguousTemplateGroup.Single().Info.Tags["language"].ChoicesAndDescriptions.Keys.FirstOrDefault());
            Assert.Equal(1, matchResult.UnambiguousTemplateGroup.Count);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_PartialMatch_HasLanguageMismatch))]
        public void TestGetTemplateResolutionResult_PartialMatch_HasLanguageMismatch()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App",
                Identity = "Console.App.T1",
                GroupIdentity = "Console.App.Test",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L1") },
                    { "type", ResolutionTestHelper.CreateTestCacheTag("project")}
                },
                BaselineInfo = new Dictionary<string, IBaselineInfo>()
                {
                    { "app", new BaselineInfo() },
                    { "standard", new BaselineInfo() }
                }
            });

            INewCommandInput userInputs = new MockNewCommandInput()
            {
                TemplateName = "console",
                IsListFlagSpecified = true,
                Language = "L2"
            };

            ListOrHelpTemplateListResolutionResult matchResult = TemplateListResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.False(matchResult.HasExactMatches);
            Assert.Equal(0, matchResult.ExactMatchedTemplatesGrouped.Count);
            Assert.Equal(0, matchResult.ExactMatchedTemplates.Count);
            Assert.Equal(1, matchResult.PartiallyMatchedTemplates.Count);
            Assert.Equal(1, matchResult.PartiallyMatchedTemplatesGrouped.Count);
            Assert.True(matchResult.HasLanguageMismatch);
            Assert.False(matchResult.HasContextMismatch);
            Assert.False(matchResult.HasBaselineMismatch);
            Assert.False(matchResult.HasUnambiguousTemplateGroup);
            Assert.Equal(0, matchResult.UnambiguousTemplateGroup.Count);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_PartialMatch_HasContextMismatch))]
        public void TestGetTemplateResolutionResult_PartialMatch_HasContextMismatch()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App",
                Identity = "Console.App.T1",
                GroupIdentity = "Console.App.Test",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L1") },
                    { "type", ResolutionTestHelper.CreateTestCacheTag("project")}
                },
                BaselineInfo = new Dictionary<string, IBaselineInfo>()
                {
                    { "app", new BaselineInfo() },
                    { "standard", new BaselineInfo() }
                }
            });

            INewCommandInput userInputs = new MockNewCommandInput()
            {
                TemplateName = "console",
                IsListFlagSpecified = true,
                TypeFilter = "item"
            };

            ListOrHelpTemplateListResolutionResult matchResult = TemplateListResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.False(matchResult.HasExactMatches);
            Assert.Equal(0, matchResult.ExactMatchedTemplatesGrouped.Count);
            Assert.Equal(0, matchResult.ExactMatchedTemplates.Count);
            Assert.Equal(1, matchResult.PartiallyMatchedTemplates.Count);
            Assert.Equal(1, matchResult.PartiallyMatchedTemplatesGrouped.Count);
            Assert.False(matchResult.HasLanguageMismatch);
            Assert.True(matchResult.HasContextMismatch);
            Assert.False(matchResult.HasBaselineMismatch);
            Assert.False(matchResult.HasUnambiguousTemplateGroup);
            Assert.Equal(0, matchResult.UnambiguousTemplateGroup.Count);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_PartialMatch_HasBaselineMismatch))]
        public void TestGetTemplateResolutionResult_PartialMatch_HasBaselineMismatch()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App",
                Identity = "Console.App.T1",
                GroupIdentity = "Console.App.Test",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L1") },
                    { "type", ResolutionTestHelper.CreateTestCacheTag("project")}
                },
                BaselineInfo = new Dictionary<string, IBaselineInfo>()
                {
                    { "app", new BaselineInfo() },
                    { "standard", new BaselineInfo() }
                }
            });

            INewCommandInput userInputs = new MockNewCommandInput()
            {
                TemplateName = "console",
                IsListFlagSpecified = true,
                BaselineName = "core"
            };

            ListOrHelpTemplateListResolutionResult matchResult = TemplateListResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.False(matchResult.HasExactMatches);
            Assert.Equal(0, matchResult.ExactMatchedTemplatesGrouped.Count);
            Assert.Equal(0, matchResult.ExactMatchedTemplates.Count);
            Assert.Equal(1, matchResult.PartiallyMatchedTemplates.Count);
            Assert.Equal(1, matchResult.PartiallyMatchedTemplatesGrouped.Count);
            Assert.False(matchResult.HasLanguageMismatch);
            Assert.False(matchResult.HasContextMismatch);
            Assert.True(matchResult.HasBaselineMismatch);
            Assert.False(matchResult.HasUnambiguousTemplateGroup);
            Assert.Equal(0, matchResult.UnambiguousTemplateGroup.Count);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_PartialMatch_HasMultipleMismatches))]
        public void TestGetTemplateResolutionResult_PartialMatch_HasMultipleMismatches()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App",
                Identity = "Console.App.T1",
                GroupIdentity = "Console.App.Test",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L1") },
                    { "type", ResolutionTestHelper.CreateTestCacheTag("project")}
                },
                BaselineInfo = new Dictionary<string, IBaselineInfo>()
                {
                    { "app", new BaselineInfo() },
                    { "standard", new BaselineInfo() }
                }
            });

            INewCommandInput userInputs = new MockNewCommandInput()
            {
                TemplateName = "console",
                IsListFlagSpecified = true,
                Language = "L2",
                TypeFilter = "item",
                BaselineName = "core"
            };

            ListOrHelpTemplateListResolutionResult matchResult = TemplateListResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.False(matchResult.HasExactMatches);
            Assert.Equal(0, matchResult.ExactMatchedTemplatesGrouped.Count);
            Assert.Equal(0, matchResult.ExactMatchedTemplates.Count);
            Assert.Equal(1, matchResult.PartiallyMatchedTemplates.Count);
            Assert.Equal(1, matchResult.PartiallyMatchedTemplatesGrouped.Count);
            Assert.True(matchResult.HasLanguageMismatch);
            Assert.True(matchResult.HasContextMismatch);
            Assert.True(matchResult.HasBaselineMismatch);
            Assert.False(matchResult.HasUnambiguousTemplateGroup);
            Assert.Equal(0, matchResult.UnambiguousTemplateGroup.Count);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_NoMatch))]
        public void TestGetTemplateResolutionResult_NoMatch()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App",
                Identity = "Console.App.T1",
                GroupIdentity = "Console.App.Test",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L1") },
                    { "type", ResolutionTestHelper.CreateTestCacheTag("project")}
                },
                BaselineInfo = new Dictionary<string, IBaselineInfo>()
                {
                    { "app", new BaselineInfo() },
                    { "standard", new BaselineInfo() }
                }
            });


            INewCommandInput userInputs = new MockNewCommandInput()
            {
                TemplateName = "zzzzz",
                IsListFlagSpecified = true,
                Language = "L1",
                TypeFilter = "project",
                BaselineName = "app"
            };

            ListOrHelpTemplateListResolutionResult matchResult = TemplateListResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.False(matchResult.HasExactMatches);
            Assert.False(matchResult.HasPartialMatches);
            Assert.Equal(0, matchResult.ExactMatchedTemplatesGrouped.Count);
            Assert.Equal(0, matchResult.ExactMatchedTemplates.Count);
            Assert.Equal(0, matchResult.PartiallyMatchedTemplates.Count);
            Assert.Equal(0, matchResult.PartiallyMatchedTemplatesGrouped.Count);
            Assert.False(matchResult.HasLanguageMismatch);
            Assert.False(matchResult.HasContextMismatch);
            Assert.False(matchResult.HasBaselineMismatch);
            Assert.False(matchResult.HasUnambiguousTemplateGroup);
            Assert.Equal(0, matchResult.UnambiguousTemplateGroup.Count);
        }



        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_MatchByTags))]
        public void TestGetTemplateResolutionResult_MatchByTags()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App",
                Identity = "Console.App.T1",
                GroupIdentity = "Console.App.Test",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Classifications = new List <string> {  "Common", "Test" },
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L1") },
                    { "type", ResolutionTestHelper.CreateTestCacheTag("project")}
                },
                BaselineInfo = new Dictionary<string, IBaselineInfo>()
                {
                    { "app", new BaselineInfo() },
                    { "standard", new BaselineInfo() }
                }
            });


            INewCommandInput userInputs = new MockNewCommandInput()
            {
                TemplateName = "Common",
                IsListFlagSpecified = true,
            };

            ListOrHelpTemplateListResolutionResult matchResult = TemplateListResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.True(matchResult.HasExactMatches);
            Assert.Equal(1, matchResult.ExactMatchedTemplatesGrouped.Count);
            Assert.Equal(1, matchResult.ExactMatchedTemplates.Count);
            Assert.False(matchResult.HasLanguageMismatch);
            Assert.False(matchResult.HasContextMismatch);
            Assert.False(matchResult.HasBaselineMismatch);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_MatchByTagsIgnoredOnNameMatch))]
        public void TestGetTemplateResolutionResult_MatchByTagsIgnoredOnNameMatch()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console1",
                Name = "Long name for Console App Test",
                Identity = "Console.App.T1",
                GroupIdentity = "Console.App.Test",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Classifications = new List<string> { "Common", "Test" },
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L1") },
                    { "type", ResolutionTestHelper.CreateTestCacheTag("project")}
                },
                BaselineInfo = new Dictionary<string, IBaselineInfo>()
                {
                    { "app", new BaselineInfo() },
                    { "standard", new BaselineInfo() }
                }
            });

            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console2",
                Name = "Long name for Console App",
                Identity = "Console.App.T2",
                GroupIdentity = "Console.App.Test2",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Classifications = new List<string> { "Common", "Test" },
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L1") },
                    { "type", ResolutionTestHelper.CreateTestCacheTag("project")}
                },
                BaselineInfo = new Dictionary<string, IBaselineInfo>()
                {
                    { "app", new BaselineInfo() },
                    { "standard", new BaselineInfo() }
                }
            });


            INewCommandInput userInputs = new MockNewCommandInput()
            {
                TemplateName = "Test",
                IsListFlagSpecified = true,
            };

            ListOrHelpTemplateListResolutionResult matchResult = TemplateListResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.True(matchResult.HasExactMatches);
            Assert.Equal(1, matchResult.ExactMatchedTemplatesGrouped.Count);
            Assert.Equal(1, matchResult.ExactMatchedTemplates.Count);
            Assert.False(matchResult.HasLanguageMismatch);
            Assert.False(matchResult.HasContextMismatch);
            Assert.False(matchResult.HasBaselineMismatch);
            Assert.Equal("console1", matchResult.UnambiguousTemplateGroup.Single().Info.ShortName);
            Assert.Equal("Console.App.T1", matchResult.UnambiguousTemplateGroup.Single().Info.Identity);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_MatchByTagsIgnoredOnShortNameMatch))]
        public void TestGetTemplateResolutionResult_MatchByTagsIgnoredOnShortNameMatch()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App Test",
                Identity = "Console.App.T1",
                GroupIdentity = "Console.App.Test",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Classifications = new List<string> { "Common", "Test", "Console" },
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L1") },
                    { "type", ResolutionTestHelper.CreateTestCacheTag("project")}
                },
                BaselineInfo = new Dictionary<string, IBaselineInfo>()
                {
                    { "app", new BaselineInfo() },
                    { "standard", new BaselineInfo() }
                }
            });

            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "cons",
                Name = "Long name for Cons App",
                Identity = "Console.App.T2",
                GroupIdentity = "Console.App.Test2",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Classifications = new List<string> { "Common", "Test", "Console" },
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L1") },
                    { "type", ResolutionTestHelper.CreateTestCacheTag("project")}
                },
                BaselineInfo = new Dictionary<string, IBaselineInfo>()
                {
                    { "app", new BaselineInfo() },
                    { "standard", new BaselineInfo() }
                }
            });


            INewCommandInput userInputs = new MockNewCommandInput()
            {
                TemplateName = "Console",
                IsListFlagSpecified = true,
            };

            ListOrHelpTemplateListResolutionResult matchResult = TemplateListResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.True(matchResult.HasExactMatches);
            Assert.Equal(1, matchResult.ExactMatchedTemplatesGrouped.Count);
            Assert.Equal(1, matchResult.ExactMatchedTemplates.Count);
            Assert.False(matchResult.HasLanguageMismatch);
            Assert.False(matchResult.HasContextMismatch);
            Assert.False(matchResult.HasBaselineMismatch);
            Assert.Equal("console", matchResult.UnambiguousTemplateGroup.Single().Info.ShortName);
            Assert.Equal("Console.App.T1", matchResult.UnambiguousTemplateGroup.Single().Info.Identity);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_MatchByTagsAndMismatchByFilter))]
        public void TestGetTemplateResolutionResult_MatchByTagsAndMismatchByFilter()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App",
                Identity = "Console.App.T1",
                GroupIdentity = "Console.App.Test",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Classifications = new List<string> { "Common", "Test" },
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L1") },
                    { "type", ResolutionTestHelper.CreateTestCacheTag("project")}
                },
                BaselineInfo = new Dictionary<string, IBaselineInfo>()
                {
                    { "app", new BaselineInfo() },
                    { "standard", new BaselineInfo() }
                }
            });


            INewCommandInput userInputs = new MockNewCommandInput()
            {
                TemplateName = "Common",
                IsListFlagSpecified = true,
                Language = "L2",
                TypeFilter = "item",
            };

            ListOrHelpTemplateListResolutionResult matchResult = TemplateListResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.False(matchResult.HasExactMatches);
            Assert.Equal(0, matchResult.ExactMatchedTemplatesGrouped.Count);
            Assert.Equal(0, matchResult.ExactMatchedTemplates.Count);
            Assert.True(matchResult.HasPartialMatches);
            Assert.Equal(1, matchResult.PartiallyMatchedTemplates.Count);
            Assert.Equal(1, matchResult.PartiallyMatchedTemplatesGrouped.Count);
            Assert.True(matchResult.HasLanguageMismatch);
            Assert.True(matchResult.HasContextMismatch);
            Assert.False(matchResult.HasBaselineMismatch);
        }

        [Theory(DisplayName = nameof(TestGetTemplateResolutionResult_AuthorMatch))]
        [InlineData("TestAuthor", "Test", true)]
        [InlineData("TestAuthor", "Other", false)]
        [InlineData("TestAuthor", "", true)]
        [InlineData("TestAuthor", null, true)]
        [InlineData("TestAuthor", "TeST", true)]
        [InlineData("TestAuthor", "Teşt", false)]
        [InlineData("match_middle_test", "middle", true)]
        [InlineData("input", "İnput", false)]
        public void TestGetTemplateResolutionResult_AuthorMatch(string templateAuthor, string commandAuthor, bool matchExpected)
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App",
                Identity = "Console.App.T1",
                GroupIdentity = "Console.App.Test",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Classifications = new List<string> { "Common", "Test" },
                Author = templateAuthor,
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L1") },
                    { "type", ResolutionTestHelper.CreateTestCacheTag("project")}
                },
                BaselineInfo = new Dictionary<string, IBaselineInfo>()
                {
                    { "app", new BaselineInfo() },
                    { "standard", new BaselineInfo() }
                }
            });


            INewCommandInput userInputs = new MockNewCommandInput()
            {
                TemplateName = "console",
                IsListFlagSpecified = true,
                AuthorFilter = commandAuthor
            };

            ListOrHelpTemplateListResolutionResult matchResult = TemplateListResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);

            if (matchExpected)
            {
                Assert.True(matchResult.HasExactMatches);
                Assert.Equal(1, matchResult.ExactMatchedTemplatesGrouped.Count);
                Assert.Equal(1, matchResult.ExactMatchedTemplates.Count);
                Assert.False(matchResult.HasPartialMatches);
                Assert.Equal(0, matchResult.PartiallyMatchedTemplates.Count);
                Assert.Equal(0, matchResult.PartiallyMatchedTemplatesGrouped.Count);
                Assert.False(matchResult.HasAuthorMismatch);
            }
            else
            {
                Assert.False(matchResult.HasExactMatches);
                Assert.Equal(0, matchResult.ExactMatchedTemplatesGrouped.Count);
                Assert.Equal(0, matchResult.ExactMatchedTemplates.Count);
                Assert.True(matchResult.HasPartialMatches);
                Assert.Equal(1, matchResult.PartiallyMatchedTemplates.Count);
                Assert.Equal(1, matchResult.PartiallyMatchedTemplatesGrouped.Count);
                Assert.True(matchResult.HasAuthorMismatch);
            }

            Assert.False(matchResult.HasLanguageMismatch);
            Assert.False(matchResult.HasContextMismatch);
            Assert.False(matchResult.HasBaselineMismatch);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_TemplateWithoutTypeShouldNotBeMatchedForContextFilter))]
        public void TestGetTemplateResolutionResult_TemplateWithoutTypeShouldNotBeMatchedForContextFilter()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App",
                Identity = "Console.App.T1",
                GroupIdentity = "Console.App.Test",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Classifications = new List<string> { "Common", "Test" },
                Tags = new Dictionary<string, ICacheTag>()
            });


            INewCommandInput userInputs = new MockNewCommandInput()
            {
                TemplateName = "Common",
                IsListFlagSpecified = true,

                TypeFilter = "item",
            };

            ListOrHelpTemplateListResolutionResult matchResult = TemplateListResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.False(matchResult.HasExactMatches);
            Assert.Equal(0, matchResult.ExactMatchedTemplatesGrouped.Count);
            Assert.Equal(0, matchResult.ExactMatchedTemplates.Count);
            Assert.True(matchResult.HasPartialMatches);
            Assert.Equal(1, matchResult.PartiallyMatchedTemplates.Count);
            Assert.Equal(1, matchResult.PartiallyMatchedTemplatesGrouped.Count);
            Assert.False(matchResult.HasLanguageMismatch);
            Assert.True(matchResult.HasContextMismatch);
            Assert.False(matchResult.HasBaselineMismatch);
        }
    }
}
