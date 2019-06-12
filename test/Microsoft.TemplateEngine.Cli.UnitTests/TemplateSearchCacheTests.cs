using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Cli.TemplateSearch;
using Microsoft.TemplateEngine.Cli.UnitTests.CliMocks;
using Microsoft.TemplateEngine.Cli.UnitTests.TemplateResolutionTests;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateSearch.Common;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class TemplateSearchCacheTests : TestBase
    {
        [Fact(DisplayName = nameof(CacheSearchNameMatchTest))]
        public void CacheSearchNameMatchTest()
        {
            TemplateDiscoveryMetadata mockTemplateDiscoveryMetadata = SetupDiscoveryMetadata(false);
            MockCliNuGetMetadataSearchSource.SetupMockData(mockTemplateDiscoveryMetadata);
            EngineEnvironmentSettings.SettingsLoader.Components.Register(typeof(MockCliNuGetMetadataSearchSource));

            const string searchTemplateName = "foo";

            TemplateSearcher searcher = new TemplateSearcher(EngineEnvironmentSettings, "C#", MockTemplateSearchHelpers.DefaultMatchFilter);
            List<IInstallUnitDescriptor> existingInstalls = new List<IInstallUnitDescriptor>();

            SearchResults searchResults = searcher.SearchForTemplatesAsync(existingInstalls, searchTemplateName).Result;
            Assert.True(searchResults.AnySources);
            Assert.Equal(1, searchResults.MatchesBySource.Count);
            Assert.Equal(2, searchResults.MatchesBySource[0].PacksWithMatches.Count);
            Assert.Single(searchResults.MatchesBySource[0].PacksWithMatches[_packOneInfo].TemplateMatches.Where(t => string.Equals(t.Info.Name, _fooOneTemplate.Name)));
            Assert.Single(searchResults.MatchesBySource[0].PacksWithMatches[_packTwoInfo].TemplateMatches.Where(t => string.Equals(t.Info.Name, _fooTwoTemplate.Name)));
        }

        [Fact(DisplayName = nameof(CacheSearchCliSymbolNameFilterTest))]
        public void CacheSearchCliSymbolNameFilterTest()
        {
            TemplateDiscoveryMetadata mockTemplateDiscoveryMetadata = SetupDiscoveryMetadata(true);

            MockCliNuGetMetadataSearchSource.SetupMockData(mockTemplateDiscoveryMetadata);
            EngineEnvironmentSettings.SettingsLoader.Components.Register(typeof(MockCliNuGetMetadataSearchSource));

            const string searchTemplateName = "foo";
            const string defaultLanguage = "C#";

            Dictionary<string, string> rawCommandInputs = new Dictionary<string, string>()
            {
                { "framework", "netcoreapp2.0" }
            };
            INewCommandInput commandInput = new MockNewCommandInput(rawCommandInputs);

            Func<IReadOnlyList<ITemplateNameSearchResult>, IReadOnlyList<Edge.Template.ITemplateMatchInfo>> matchFilter = new CliHostSpecificDataMatchFilterFactory(commandInput, defaultLanguage).MatchFilter;

            TemplateSearcher searcher = new TemplateSearcher(EngineEnvironmentSettings, defaultLanguage, matchFilter);
            List<IInstallUnitDescriptor> existingInstalls = new List<IInstallUnitDescriptor>();

            SearchResults searchResults = searcher.SearchForTemplatesAsync(existingInstalls, searchTemplateName).Result;
            Assert.True(searchResults.AnySources);
            Assert.Equal(1, searchResults.MatchesBySource.Count);
            Assert.Equal(1, searchResults.MatchesBySource[0].PacksWithMatches.Count);
            Assert.Single(searchResults.MatchesBySource[0].PacksWithMatches[_packTwoInfo].TemplateMatches.Where(t => string.Equals(t.Info.Name, _fooTwoTemplate.Name)));
        }

        [Fact(DisplayName = nameof(CacheSearchLanguageFilterTest))]
        public void CacheSearchLanguageFilterTest()
        {
            TemplateDiscoveryMetadata mockTemplateDiscoveryMetadata = SetupDiscoveryMetadata(false);

            MockCliNuGetMetadataSearchSource.SetupMockData(mockTemplateDiscoveryMetadata);
            EngineEnvironmentSettings.SettingsLoader.Components.Register(typeof(MockCliNuGetMetadataSearchSource));

            const string searchTemplateName = "bar";
            const string defaultLanguage = "C#";

            Dictionary<string, string> rawCommandInputs = new Dictionary<string, string>();
            MockNewCommandInput commandInput = new MockNewCommandInput(rawCommandInputs);
            commandInput.Language = "F#";

            Func<IReadOnlyList<ITemplateNameSearchResult>, IReadOnlyList<Edge.Template.ITemplateMatchInfo>> matchFilter = new CliHostSpecificDataMatchFilterFactory(commandInput, defaultLanguage).MatchFilter;

            TemplateSearcher searcher = new TemplateSearcher(EngineEnvironmentSettings, defaultLanguage, matchFilter);
            List<IInstallUnitDescriptor> existingInstalls = new List<IInstallUnitDescriptor>();

            SearchResults searchResults = searcher.SearchForTemplatesAsync(existingInstalls, searchTemplateName).Result;
            Assert.True(searchResults.AnySources);
            Assert.Equal(1, searchResults.MatchesBySource.Count);
            Assert.Equal(1, searchResults.MatchesBySource[0].PacksWithMatches.Count);
            Assert.Single(searchResults.MatchesBySource[0].PacksWithMatches[_packThreeInfo].TemplateMatches.Where(t => string.Equals(t.Info.Name, _barFSharpTemplate.Name)));
        }

        private static readonly PackInfo _packOneInfo = new PackInfo("PackOne", "1.0.0");
        private static readonly PackInfo _packTwoInfo = new PackInfo("PackTwo", "1.6.0");
        private static readonly PackInfo _packThreeInfo = new PackInfo("PackThree", "2.1");

        private static readonly ITemplateInfo _fooOneTemplate = new TemplateInfo()
        {
            Identity = "Mock.Foo.1",
            GroupIdentity = "Mock.Foo",
            Description = "Mock Foo template one",
            Name = "MockFooTemplateOne",
            ShortName = "foo1",
            Tags = new Dictionary<string, ICacheTag>(StringComparer.Ordinal)
            {
                {
                    "Framework", ResolutionTestHelper.CreateTestCacheTag(new List<string>() { "netcoreapp3.0", "netcoreapp3.1" })
                },
                {
                    "language", ResolutionTestHelper.CreateTestCacheTag(new List<string>() { "C#" })
                }
            },
            CacheParameters = new Dictionary<string, ICacheParameter>()
        };

        private static readonly ITemplateInfo _fooTwoTemplate = new TemplateInfo()
        {
            Identity = "Mock.Foo.2",
            GroupIdentity = "Mock.Foo",
            Description = "Mock Foo template two",
            Name = "MockFooTemplateTwo",
            ShortName = "foo2",
            Tags = new Dictionary<string, ICacheTag>(StringComparer.Ordinal)
            {
                {
                    "Framework", ResolutionTestHelper.CreateTestCacheTag(new List<string>() { "netcoreapp2.0", "netcoreapp2.1" })
                },
                {
                    "language", ResolutionTestHelper.CreateTestCacheTag(new List<string>() { "C#" })
                }
            },
            CacheParameters = new Dictionary<string, ICacheParameter>()
        };

        private static readonly ITemplateInfo _barCSharpTemplate = new MockTemplateInfo()
        {
            Identity = "Mock.Bar.1.Csharp",
            GroupIdentity = "Mock.Bar",
            Description = "Mock Bar CSharp template",
            Name = "MockBarCsharpTemplate",
            ShortName = "barC",
            Tags = new Dictionary<string, ICacheTag>(StringComparer.Ordinal)
            {
                {
                    "language", ResolutionTestHelper.CreateTestCacheTag(new List<string>() { "C#" })
                }
            },
            CacheParameters = new Dictionary<string, ICacheParameter>()
        };

        private static readonly ITemplateInfo _barFSharpTemplate = new MockTemplateInfo()
        {
            Identity = "Mock.Bar.1.FSharp",
            GroupIdentity = "Mock.Bar",
            Description = "Mock Bar FSharp template",
            Name = "MockBarFSharpTemplate",
            ShortName = "barF",
            Tags = new Dictionary<string, ICacheTag>(StringComparer.Ordinal)
            {
                {
                    "language", ResolutionTestHelper.CreateTestCacheTag(new List<string>() { "F#" })
                }
            },
            CacheParameters = new Dictionary<string, ICacheParameter>()
        };

        private static TemplateDiscoveryMetadata SetupDiscoveryMetadata(bool includehostData = false)
        {
            const string version = "1.0.0.0";

            List<ITemplateInfo> templateCache = new List<ITemplateInfo>();

            templateCache.Add(_fooOneTemplate);
            templateCache.Add(_fooTwoTemplate);
            templateCache.Add(_barCSharpTemplate);
            templateCache.Add(_barFSharpTemplate);

            Dictionary<string, PackToTemplateEntry> packToTemplateMap = new Dictionary<string, PackToTemplateEntry>();

            List<TemplateIdentificationEntry> packOneTemplateInfo = new List<TemplateIdentificationEntry>()
            {
                new TemplateIdentificationEntry(_fooOneTemplate.Identity, _fooOneTemplate.GroupIdentity)
            };
            packToTemplateMap[_packOneInfo.Name] = new PackToTemplateEntry(_packOneInfo.Version, packOneTemplateInfo);

            List<TemplateIdentificationEntry> packTwoTemplateInfo = new List<TemplateIdentificationEntry>()
            {
                new TemplateIdentificationEntry(_fooTwoTemplate.Identity, _fooTwoTemplate.GroupIdentity)
            };
            packToTemplateMap[_packTwoInfo.Name] = new PackToTemplateEntry(_packTwoInfo.Version, packTwoTemplateInfo);

            List<TemplateIdentificationEntry> packThreeTemplateInfo = new List<TemplateIdentificationEntry>()
            {
                new TemplateIdentificationEntry(_barCSharpTemplate.Identity, _barCSharpTemplate.GroupIdentity),
                new TemplateIdentificationEntry(_barFSharpTemplate.Identity, _barFSharpTemplate.GroupIdentity)
            };
            packToTemplateMap[_packThreeInfo.Name] = new PackToTemplateEntry(_packThreeInfo.Version, packThreeTemplateInfo);

            Dictionary<string, object> additionalData = new Dictionary<string, object>();

            if (includehostData)
            {
                Dictionary<string, string> frameworkParamSymbolInfo = new Dictionary<string, string>()
                {
                    { "longName", "framework" },
                    { "shortName", "f" }
                };

                HostSpecificTemplateData fooTemplateHostData = new MockHostSpecificTemplateData(
                    new Dictionary<string, IReadOnlyDictionary<string, string>>()
                    {
                        { "Framework", frameworkParamSymbolInfo }
                    }
                );

                Dictionary<string, HostSpecificTemplateData> cliHostData = new Dictionary<string, HostSpecificTemplateData>()
                {
                    { _fooOneTemplate.Identity, fooTemplateHostData },
                    { _fooTwoTemplate.Identity, fooTemplateHostData }
                };

                additionalData[CliNuGetSearchCacheConfig.CliHostDataName] = cliHostData;
            }

            TemplateDiscoveryMetadata discoveryMetadta = new TemplateDiscoveryMetadata(version, templateCache, packToTemplateMap, additionalData);
        
            return discoveryMetadta;
        }
    }
}
