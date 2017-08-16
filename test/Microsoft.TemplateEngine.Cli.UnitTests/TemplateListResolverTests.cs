using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
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

        [Fact(DisplayName = nameof(TestFindHighestPrecedenceTemplateIfAllSameGroupIdentity_ReturnsNullWithDifferentGroups))]
        public void TestFindHighestPrecedenceTemplateIfAllSameGroupIdentity_ReturnsNullWithDifferentGroups()
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

            IReadOnlyCollection<IFilteredTemplateInfo> projectTemplates = TemplateListResolver.PerformAllTemplatesInContextQuery(templatesToSearch, null, "project");
            Assert.Equal(3, projectTemplates.Count);
            Assert.True(projectTemplates.Where(x => string.Equals(x.Info.Identity, "Template1", StringComparison.Ordinal)).Any());
            Assert.True(projectTemplates.Where(x => string.Equals(x.Info.Identity, "Template4", StringComparison.Ordinal)).Any());
            Assert.True(projectTemplates.Where(x => string.Equals(x.Info.Identity, "Template5", StringComparison.Ordinal)).Any());

            IReadOnlyCollection<IFilteredTemplateInfo> itemTemplates = TemplateListResolver.PerformAllTemplatesInContextQuery(templatesToSearch, null, "item");
            Assert.Equal(1, itemTemplates.Count);
            Assert.True(itemTemplates.Where(x => string.Equals(x.Info.Identity, "Template2", StringComparison.Ordinal)).Any());

            IReadOnlyCollection<IFilteredTemplateInfo> otherTemplates = TemplateListResolver.PerformAllTemplatesInContextQuery(templatesToSearch, null, "other");
            Assert.Equal(1, otherTemplates.Count);
            Assert.True(otherTemplates.Where(x => string.Equals(x.Info.Identity, "Template3", StringComparison.Ordinal)).Any());
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
    }
}
