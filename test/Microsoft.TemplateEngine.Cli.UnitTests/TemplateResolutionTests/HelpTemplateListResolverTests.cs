// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Xunit;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Cli.UnitTests.CliMocks;
using System.Linq;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects;
using Microsoft.TemplateEngine.Cli.HelpAndUsage;

namespace Microsoft.TemplateEngine.Cli.UnitTests.TemplateResolutionTests
{
    public class HelpTemplateListResolverTests
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
                IsHelpFlagSpecified = true
            };

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
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
                IsHelpFlagSpecified = true
            };

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.True(matchResult.HasExactMatches);
            Assert.True(matchResult.HasUnambiguousTemplateGroup);
            Assert.Equal("console", matchResult.UnambiguousTemplateGroup.Single().Info.ShortName);
            Assert.Equal("Console.App", matchResult.UnambiguousTemplateGroup.Single().Info.Identity);
            Assert.Equal(1, matchResult.UnambiguousTemplateGroup.Count);
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
                IsHelpFlagSpecified = true
            };

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
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
                IsHelpFlagSpecified = true
            };

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
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
                IsHelpFlagSpecified = true
            };

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, "L1");
            Assert.True(matchResult.HasExactMatches);
            Assert.True(matchResult.HasUnambiguousTemplateGroup);
            Assert.True(matchResult.HasUnambiguousTemplateGroupForDefaultLanguage);
            Assert.Equal("console", matchResult.UnambiguousTemplatesForDefaultLanguage.Single().Info.ShortName);
            Assert.Equal("Console.App.L1", matchResult.UnambiguousTemplatesForDefaultLanguage.Single().Info.Identity);
            Assert.Equal("L1", matchResult.UnambiguousTemplatesForDefaultLanguage.Single().Info.Tags["language"].ChoicesAndDescriptions.Keys.FirstOrDefault());
            Assert.Equal(2, matchResult.UnambiguousTemplateGroup.Count);
            Assert.Equal(1, matchResult.UnambiguousTemplatesForDefaultLanguage.Count);
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
                IsHelpFlagSpecified = true,
                Language = "L2"
            };

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, "L1");
            Assert.True(matchResult.HasExactMatches);
            Assert.True(matchResult.HasUnambiguousTemplateGroup);
            Assert.Equal("console", matchResult.UnambiguousTemplateGroup.Single().Info.ShortName);
            Assert.Equal("Console.App.L2", matchResult.UnambiguousTemplateGroup.Single().Info.Identity);
            Assert.Equal("L2", matchResult.UnambiguousTemplateGroup.Single().Info.Tags["language"].ChoicesAndDescriptions.Keys.FirstOrDefault());
            Assert.Equal(1, matchResult.UnambiguousTemplateGroup.Count);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_UnambiguousGroup_TemplatesAreNotSameLanguage))]
        public void TestGetTemplateResolutionResult_UnambiguousGroup_TemplatesAreNotSameLanguage()
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
                IsHelpFlagSpecified = true
            };

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.True(matchResult.HasExactMatches);
            Assert.Equal(1, matchResult.ExactMatchedTemplatesGrouped.Count);
            Assert.Equal(3, matchResult.ExactMatchedTemplates.Count);
            Assert.True(matchResult.HasUnambiguousTemplateGroup);
            Assert.Equal(3, matchResult.UnambiguousTemplateGroup.Count);
            Assert.False(matchResult.AllTemplatesInUnambiguousTemplateGroupAreSameLanguage);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_UnambiguousGroup_TemplatesAreSameLanguage))]
        public void TestGetTemplateResolutionResult_UnambiguousGroup_TemplatesAreSameLanguage()
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
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L1") }
                }
            });
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App",
                Identity = "Console.App.T2",
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
                Identity = "Console.App.T3",
                GroupIdentity = "Console.App.Test",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L1") }
                }
            });

            INewCommandInput userInputs = new MockNewCommandInput()
            {
                TemplateName = "console",
                IsHelpFlagSpecified = true
            };

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.True(matchResult.HasExactMatches);
            Assert.Equal(1, matchResult.ExactMatchedTemplatesGrouped.Count);
            Assert.Equal(3, matchResult.ExactMatchedTemplates.Count);
            Assert.True(matchResult.HasUnambiguousTemplateGroup);
            Assert.Equal(3, matchResult.UnambiguousTemplateGroup.Count);
            Assert.True(matchResult.AllTemplatesInUnambiguousTemplateGroupAreSameLanguage);
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
                IsHelpFlagSpecified = true,
                Language = "L2"
            };

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
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
                IsHelpFlagSpecified = true,
                TypeFilter = "item"
            };

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.False(matchResult.HasExactMatches);
            Assert.Equal(0, matchResult.ExactMatchedTemplatesGrouped.Count);
            Assert.Equal(0, matchResult.ExactMatchedTemplates.Count);
            Assert.Equal(1, matchResult.PartiallyMatchedTemplates.Count);
            Assert.Equal(1, matchResult.PartiallyMatchedTemplatesGrouped.Count);
            Assert.False(matchResult.HasLanguageMismatch);
            Assert.True (matchResult.HasContextMismatch);
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
                IsHelpFlagSpecified = true,
                BaselineName = "core"
            };

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
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
                IsHelpFlagSpecified = true,
                Language = "L2",
                TypeFilter = "item",
                BaselineName = "core"
            };

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
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

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_PartialMatchGroup_HasTypeMismatch_HasGroupLanguageMatch))]
        public void TestGetTemplateResolutionResult_PartialMatchGroup_HasTypeMismatch_HasGroupLanguageMatch()
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

            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App",
                Identity = "Console.App.T2",
                GroupIdentity = "Console.App.Test",
                CacheParameters = new Dictionary<string, ICacheParameter>(),
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L2") },
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
                IsHelpFlagSpecified = true,
                Language = "L2",
                TypeFilter = "item"
            };

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.False(matchResult.HasExactMatches);
            Assert.Equal(0, matchResult.ExactMatchedTemplatesGrouped.Count);
            Assert.Equal(0, matchResult.ExactMatchedTemplates.Count);
            Assert.Equal(2, matchResult.PartiallyMatchedTemplates.Count);
            Assert.Equal(1, matchResult.PartiallyMatchedTemplatesGrouped.Count);
            Assert.False(matchResult.HasLanguageMismatch);
            Assert.True(matchResult.HasContextMismatch);
            Assert.False(matchResult.HasBaselineMismatch);
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
                IsHelpFlagSpecified = true,
                Language = "L1",
                TypeFilter = "project",
                BaselineName = "app"
            };

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
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


        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_OtherParameterMatch_Text))]
        public void TestGetTemplateResolutionResult_OtherParameterMatch_Text()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App",
                Identity = "Console.App.T1",
                GroupIdentity = "Console.App.Test1",
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L1") },
                    { "type", ResolutionTestHelper.CreateTestCacheTag("project")}
                },
                BaselineInfo = new Dictionary<string, IBaselineInfo>()
                {
                    { "app", new BaselineInfo() },
                    { "standard", new BaselineInfo() }
                },
                CacheParameters = new Dictionary<string, ICacheParameter>(StringComparer.OrdinalIgnoreCase)
                {
                    { "langVersion", new CacheParameter("text", "", "test description") }
                }

            });

            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App",
                Identity = "Console.App.T2",
                GroupIdentity = "Console.App.Test2",
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L2") },
                    { "type", ResolutionTestHelper.CreateTestCacheTag("project")}
                },
                BaselineInfo = new Dictionary<string, IBaselineInfo>()
                {
                    { "app", new BaselineInfo() },
                    { "standard", new BaselineInfo() }
                },
                CacheParameters = new Dictionary<string, ICacheParameter>(StringComparer.OrdinalIgnoreCase)
                {
                    { "test", new CacheParameter("text", "", "test description") }
                }

            });

            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App",
                Identity = "Console.App.T3",
                GroupIdentity = "Console.App.Test3",
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L3") },
                    { "type", ResolutionTestHelper.CreateTestCacheTag("project")}
                },
                BaselineInfo = new Dictionary<string, IBaselineInfo>()
                {
                    { "app", new BaselineInfo() },
                    { "standard", new BaselineInfo() }
                },
                CacheParameters = new Dictionary<string, ICacheParameter>()
            });


            INewCommandInput userInputs = new MockNewCommandInput(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "langVersion", null }
                }
            )
            {
                TemplateName = "c",
                IsHelpFlagSpecified = true,
            };

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.True(matchResult.HasExactMatches);
            Assert.Equal(1, matchResult.ExactMatchedTemplatesGrouped.Count);
            Assert.Equal(1, matchResult.ExactMatchedTemplates.Count);
            Assert.False(matchResult.HasLanguageMismatch);
            Assert.False(matchResult.HasContextMismatch);
            Assert.False(matchResult.HasBaselineMismatch);
            Assert.True(matchResult.HasUnambiguousTemplateGroup);
            Assert.Equal(1, matchResult.UnambiguousTemplateGroup.Count);
            HelpForTemplateResolution.GetParametersInvalidForTemplatesInList(matchResult.ExactMatchedTemplates, out IReadOnlyList<string> invalidForAllTemplates, out IReadOnlyList<string> invalidForSomeTemplates);
            Assert.Equal(0, invalidForSomeTemplates.Count);
            Assert.Equal(0, invalidForAllTemplates.Count);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_OtherParameterMatch_Choice))]
        public void TestGetTemplateResolutionResult_OtherParameterMatch_Choice()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App",
                Identity = "Console.App.T1",
                GroupIdentity = "Console.App.Test1",
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L1") },
                    { "type", ResolutionTestHelper.CreateTestCacheTag("project")},
                    { "framework", ResolutionTestHelper.CreateTestCacheTag(new List<string>() { "netcoreapp1.0", "netcoreapp1.1" }) }
                },
                BaselineInfo = new Dictionary<string, IBaselineInfo>()
                {
                    { "app", new BaselineInfo() },
                    { "standard", new BaselineInfo() }
                },
                CacheParameters = new Dictionary<string, ICacheParameter>()

            });

            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App",
                Identity = "Console.App.T2",
                GroupIdentity = "Console.App.Test2",
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L2") },
                    { "type", ResolutionTestHelper.CreateTestCacheTag("project")}
                },
                BaselineInfo = new Dictionary<string, IBaselineInfo>()
                {
                    { "app", new BaselineInfo() },
                    { "standard", new BaselineInfo() }
                },
                CacheParameters = new Dictionary<string, ICacheParameter>(StringComparer.OrdinalIgnoreCase)
                {
                    { "test", new CacheParameter("text", "", "test description") }
                }

            });

            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App",
                Identity = "Console.App.T3",
                GroupIdentity = "Console.App.Test3",
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L3") },
                    { "type", ResolutionTestHelper.CreateTestCacheTag("project")}
                },
                BaselineInfo = new Dictionary<string, IBaselineInfo>()
                {
                    { "app", new BaselineInfo() },
                    { "standard", new BaselineInfo() }
                },
                CacheParameters = new Dictionary<string, ICacheParameter>()
            });


            INewCommandInput userInputs = new MockNewCommandInput(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "framework", null }
                }
            )
            {
                TemplateName = "c",
                IsHelpFlagSpecified = true,
            };

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.True(matchResult.HasExactMatches);
            Assert.Equal(3, matchResult.ExactMatchedTemplatesGrouped.Count);
            Assert.Equal(3, matchResult.ExactMatchedTemplates.Count);
            Assert.False(matchResult.HasLanguageMismatch);
            Assert.False(matchResult.HasContextMismatch);
            Assert.False(matchResult.HasBaselineMismatch);
            Assert.False(matchResult.HasUnambiguousTemplateGroup);
            HelpForTemplateResolution.GetParametersInvalidForTemplatesInList(matchResult.ExactMatchedTemplates, out IReadOnlyList<string> invalidForAllTemplates, out IReadOnlyList<string> invalidForSomeTemplates);
            Assert.Equal(1, invalidForSomeTemplates.Count);
            Assert.Equal(0, invalidForAllTemplates.Count);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_OtherParameterDoesNotExist))]
        public void TestGetTemplateResolutionResult_OtherParameterDoesNotExist()
        {
            List<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>();
            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App",
                Identity = "Console.App.T1",
                GroupIdentity = "Console.App.Test1",
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L1") },
                    { "type", ResolutionTestHelper.CreateTestCacheTag("project")},
                    { "framework", ResolutionTestHelper.CreateTestCacheTag(new List<string>() { "netcoreapp1.0", "netcoreapp1.1" }) }
                },
                BaselineInfo = new Dictionary<string, IBaselineInfo>()
                {
                    { "app", new BaselineInfo() },
                    { "standard", new BaselineInfo() }
                },
                CacheParameters = new Dictionary<string, ICacheParameter>()

            });

            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App",
                Identity = "Console.App.T2",
                GroupIdentity = "Console.App.Test2",
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L2") },
                    { "type", ResolutionTestHelper.CreateTestCacheTag("project")}
                },
                BaselineInfo = new Dictionary<string, IBaselineInfo>()
                {
                    { "app", new BaselineInfo() },
                    { "standard", new BaselineInfo() }
                },
                CacheParameters = new Dictionary<string, ICacheParameter>(StringComparer.OrdinalIgnoreCase)
                {
                    { "test", new CacheParameter("text", "", "test description") }
                }

            });

            templatesToSearch.Add(new TemplateInfo()
            {
                ShortName = "console",
                Name = "Long name for Console App",
                Identity = "Console.App.T3",
                GroupIdentity = "Console.App.Test3",
                Tags = new Dictionary<string, ICacheTag>(StringComparer.OrdinalIgnoreCase)
                {
                    { "language", ResolutionTestHelper.CreateTestCacheTag("L3") },
                    { "type", ResolutionTestHelper.CreateTestCacheTag("project")}
                },
                BaselineInfo = new Dictionary<string, IBaselineInfo>()
                {
                    { "app", new BaselineInfo() },
                    { "standard", new BaselineInfo() }
                },
                CacheParameters = new Dictionary<string, ICacheParameter>()
            });


            INewCommandInput userInputs = new MockNewCommandInput(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "do-not-exist", null }
                }
            )
            {
                TemplateName = "c",
                IsHelpFlagSpecified = true,
            };

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.True(matchResult.HasExactMatches);
            Assert.Equal(3, matchResult.ExactMatchedTemplatesGrouped.Count);
            Assert.Equal(3, matchResult.ExactMatchedTemplates.Count);
            Assert.False(matchResult.HasLanguageMismatch);
            Assert.False(matchResult.HasContextMismatch);
            Assert.False(matchResult.HasBaselineMismatch);
            Assert.False(matchResult.HasUnambiguousTemplateGroup);
            HelpForTemplateResolution.GetParametersInvalidForTemplatesInList(matchResult.ExactMatchedTemplates, out IReadOnlyList<string> invalidForAllTemplates, out IReadOnlyList<string> invalidForSomeTemplates);
            Assert.Equal(0, invalidForSomeTemplates.Count);
            Assert.Equal(1, invalidForAllTemplates.Count);
        }

        [Fact(DisplayName = nameof(TestGetTemplateResolutionResult_MatchByTagsIgnoredForHelp))]
        public void TestGetTemplateResolutionResult_MatchByTagsIgnoredForHelp()
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
                IsHelpFlagSpecified = true,
            };

            TemplateListResolutionResult matchResult = TemplateResolver.GetTemplateResolutionResultForListOrHelp(templatesToSearch, new MockHostSpecificDataLoader(), userInputs, null);
            Assert.False(matchResult.HasExactMatches);
            Assert.False(matchResult.HasPartialMatches);
            Assert.Equal(0, matchResult.ExactMatchedTemplatesGrouped.Count);
            Assert.Equal(0, matchResult.ExactMatchedTemplates.Count);
            Assert.False(matchResult.HasLanguageMismatch);
            Assert.False(matchResult.HasContextMismatch);
            Assert.False(matchResult.HasBaselineMismatch);
        }
    }
}
