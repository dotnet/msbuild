using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
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
        private static readonly string DefaultLanguage = "C#";

        [Fact(DisplayName = nameof(CacheSearchNameMatchTest))]
        public async Task CacheSearchNameMatchTest()
        {
            TemplateDiscoveryMetadata mockTemplateDiscoveryMetadata = SetupDiscoveryMetadata(false);
            MockCliNuGetMetadataSearchSource.SetupMockData(mockTemplateDiscoveryMetadata);
            EngineEnvironmentSettings.SettingsLoader.Components.Register(typeof(MockCliNuGetMetadataSearchSource));

            INewCommandInput commandInput = new MockNewCommandInput("foo");

            TemplateSearchCoordinator searchCoordinator = CliTemplateSearchCoordinatorFactory.CreateCliTemplateSearchCoordinator(EngineEnvironmentSettings, commandInput, DefaultLanguage);
            SearchResults searchResults = await searchCoordinator.SearchAsync();

            Assert.True(searchResults.AnySources);
            Assert.Equal(1, searchResults.MatchesBySource.Count);
            Assert.Equal(2, searchResults.MatchesBySource[0].PacksWithMatches.Count);
            Assert.Single(searchResults.MatchesBySource[0].PacksWithMatches[_packOneInfo].TemplateMatches.Where(t => string.Equals(t.Info.Name, _fooOneTemplate.Name)));
            Assert.Single(searchResults.MatchesBySource[0].PacksWithMatches[_packTwoInfo].TemplateMatches.Where(t => string.Equals(t.Info.Name, _fooTwoTemplate.Name)));
        }

        // check that the symbol name-value correctly matches.
        // The _fooOneTemplate is a non-match because of a framework choice param value mismatch.
        // But the _fooTwoTemplate matches because the framework choice is valid for that template.
        [Fact(DisplayName = nameof(CacheSearchCliSymbolNameFilterTest))]
        public async Task CacheSearchCliSymbolNameFilterTest()
        {
            TemplateDiscoveryMetadata mockTemplateDiscoveryMetadata = SetupDiscoveryMetadata(true);
            MockCliNuGetMetadataSearchSource.SetupMockData(mockTemplateDiscoveryMetadata);
            EngineEnvironmentSettings.SettingsLoader.Components.Register(typeof(MockCliNuGetMetadataSearchSource));

            INewCommandInput commandInput = new MockNewCommandInput("foo").WithTemplateOption("framework", "netcoreapp2.0");

            TemplateSearchCoordinator searchCoordinator = CliTemplateSearchCoordinatorFactory.CreateCliTemplateSearchCoordinator(EngineEnvironmentSettings, commandInput, DefaultLanguage);
            SearchResults searchResults = await searchCoordinator.SearchAsync();

            Assert.True(searchResults.AnySources);
            Assert.Equal(1, searchResults.MatchesBySource.Count);
            Assert.Equal(1, searchResults.MatchesBySource[0].PacksWithMatches.Count);
            Assert.Single(searchResults.MatchesBySource[0].PacksWithMatches[_packTwoInfo].TemplateMatches.Where(t => string.Equals(t.Info.Name, _fooTwoTemplate.Name)));

            INewCommandInput shortNameCommandInput = new MockNewCommandInput("foo").WithTemplateOption("f", "netcoreapp2.0");

            TemplateSearchCoordinator shortNameSearchCoordinator = CliTemplateSearchCoordinatorFactory.CreateCliTemplateSearchCoordinator(EngineEnvironmentSettings, shortNameCommandInput, DefaultLanguage);
            SearchResults shortNameSearchResults = await searchCoordinator.SearchAsync();

            Assert.True(shortNameSearchResults.AnySources);
            Assert.Equal(1, shortNameSearchResults.MatchesBySource.Count);
            Assert.Equal(1, shortNameSearchResults.MatchesBySource[0].PacksWithMatches.Count);
            Assert.Single(shortNameSearchResults.MatchesBySource[0].PacksWithMatches[_packTwoInfo].TemplateMatches.Where(t => string.Equals(t.Info.Name, _fooTwoTemplate.Name)));
        }

        // test that an invalid symbol makes the search be a non-match
        [Fact(DisplayName = nameof(CacheSearchCliSymbolNameMismatchFilterTest))]
        public async Task CacheSearchCliSymbolNameMismatchFilterTest()
        {
            TemplateDiscoveryMetadata mockTemplateDiscoveryMetadata = SetupDiscoveryMetadata(true);
            MockCliNuGetMetadataSearchSource.SetupMockData(mockTemplateDiscoveryMetadata);
            EngineEnvironmentSettings.SettingsLoader.Components.Register(typeof(MockCliNuGetMetadataSearchSource));

            // "tfm" is not a vaild symbol for the "foo" template. So it should not match.
            INewCommandInput commandInput = new MockNewCommandInput("foo").WithTemplateOption("tfm", "netcoreapp2.0");

            TemplateSearchCoordinator searchCoordinator = CliTemplateSearchCoordinatorFactory.CreateCliTemplateSearchCoordinator(EngineEnvironmentSettings, commandInput, DefaultLanguage);
            SearchResults searchResults = await searchCoordinator.SearchAsync();

            Assert.True(searchResults.AnySources);
            Assert.Equal(0, searchResults.MatchesBySource.Count);
        }

        // Tests that the input language causes the correct match filtering.
        [Fact(DisplayName = nameof(CacheSearchLanguageFilterTest))]
        public async Task CacheSearchLanguageFilterTest()
        {
            TemplateDiscoveryMetadata mockTemplateDiscoveryMetadata = SetupDiscoveryMetadata(false);
            MockCliNuGetMetadataSearchSource.SetupMockData(mockTemplateDiscoveryMetadata);
            EngineEnvironmentSettings.SettingsLoader.Components.Register(typeof(MockCliNuGetMetadataSearchSource));

            Dictionary<string, string> rawCommandInputs = new Dictionary<string, string>();
            MockNewCommandInput commandInput = new MockNewCommandInput("bar", "F#");

            TemplateSearchCoordinator searchCoordinator = CliTemplateSearchCoordinatorFactory.CreateCliTemplateSearchCoordinator(EngineEnvironmentSettings, commandInput, DefaultLanguage);
            SearchResults searchResults = await searchCoordinator.SearchAsync();

            Assert.True(searchResults.AnySources);
            Assert.Equal(1, searchResults.MatchesBySource.Count);
            Assert.Equal(1, searchResults.MatchesBySource[0].PacksWithMatches.Count);
            Assert.Single(searchResults.MatchesBySource[0].PacksWithMatches[_packThreeInfo].TemplateMatches.Where(t => string.Equals(t.Info.Name, _barFSharpTemplate.Name)));
        }

        [Theory(DisplayName = nameof(CacheSearchAuthorFilterTest))]
        [InlineData("", "test", 1)]
        [InlineData("foo", "test", 1)]
        [InlineData("", "Wrong", 0)]
        public async Task CacheSearchAuthorFilterTest(string commandTemplate, string commandAuthor, int matchCount)
        {
            TemplateDiscoveryMetadata mockTemplateDiscoveryMetadata = SetupDiscoveryMetadata(false);
            MockCliNuGetMetadataSearchSource.SetupMockData(mockTemplateDiscoveryMetadata);
            EngineEnvironmentSettings.SettingsLoader.Components.Register(typeof(MockCliNuGetMetadataSearchSource));

            MockNewCommandInput commandInput = new MockNewCommandInput(commandTemplate).WithCommandOption("--author", commandAuthor);

            TemplateSearchCoordinator searchCoordinator = CliTemplateSearchCoordinatorFactory.CreateCliTemplateSearchCoordinator(EngineEnvironmentSettings, commandInput, DefaultLanguage);
            SearchResults searchResults = await searchCoordinator.SearchAsync();

            Assert.True(searchResults.AnySources);
            if (matchCount == 0)
            {
                Assert.Equal(0, searchResults.MatchesBySource.Count);
            }
            else
            {
                Assert.Equal(1, searchResults.MatchesBySource.Count);
                Assert.Equal(matchCount, searchResults.MatchesBySource[0].PacksWithMatches.Count);
            }
        }

        [Theory(DisplayName = nameof(CacheSearchTypeFilterTest), Skip = "skipped until type fix is merged")]
        [InlineData("", "project", 1)]
        [InlineData("foo", "project", 1)]
        [InlineData("", "Wrong", 0)]
        public async Task CacheSearchTypeFilterTest(string commandTemplate, string commandType, int matchCount)
        {
            TemplateDiscoveryMetadata mockTemplateDiscoveryMetadata = SetupDiscoveryMetadata(false);
            MockCliNuGetMetadataSearchSource.SetupMockData(mockTemplateDiscoveryMetadata);
            EngineEnvironmentSettings.SettingsLoader.Components.Register(typeof(MockCliNuGetMetadataSearchSource));

            MockNewCommandInput commandInput = new MockNewCommandInput(commandTemplate, type: commandType);

            TemplateSearchCoordinator searchCoordinator = CliTemplateSearchCoordinatorFactory.CreateCliTemplateSearchCoordinator(EngineEnvironmentSettings, commandInput, DefaultLanguage);
            SearchResults searchResults = await searchCoordinator.SearchAsync();

            Assert.True(searchResults.AnySources);
            if (matchCount == 0)
            {
                Assert.Equal(0, searchResults.MatchesBySource.Count);
            }
            else
            {
                Assert.Equal(1, searchResults.MatchesBySource.Count);
                Assert.Equal(matchCount, searchResults.MatchesBySource[0].PacksWithMatches.Count);
            }
        }

        [Theory(DisplayName = nameof(CacheSearchPackageFilterTest))]
        [InlineData("", "Three", 1, 2)]
        [InlineData("barC", "Three", 1, 1)]
        [InlineData("foo", "Three", 0, 0)]
        [InlineData("", "Wrong", 0, 0)]
        public async Task CacheSearchPackageFilterTest(string commandTemplate, string commandPackage, int packMatchCount, int templateMatchCount)
        {
            TemplateDiscoveryMetadata mockTemplateDiscoveryMetadata = SetupDiscoveryMetadata(false);
            MockCliNuGetMetadataSearchSource.SetupMockData(mockTemplateDiscoveryMetadata);
            EngineEnvironmentSettings.SettingsLoader.Components.Register(typeof(MockCliNuGetMetadataSearchSource));

            MockNewCommandInput commandInput = new MockNewCommandInput(commandTemplate).WithCommandOption("--package", commandPackage);

            TemplateSearchCoordinator searchCoordinator = CliTemplateSearchCoordinatorFactory.CreateCliTemplateSearchCoordinator(EngineEnvironmentSettings, commandInput, DefaultLanguage);
            SearchResults searchResults = await searchCoordinator.SearchAsync();

            Assert.True(searchResults.AnySources);
            if (packMatchCount == 0)
            {
                Assert.Equal(0, searchResults.MatchesBySource.Count);
            }
            else
            {
                Assert.Equal(1, searchResults.MatchesBySource.Count);
                Assert.Equal(packMatchCount, searchResults.MatchesBySource[0].PacksWithMatches.Count);
                Assert.Equal(templateMatchCount, searchResults.MatchesBySource[0].PacksWithMatches[_packThreeInfo].TemplateMatches.Count);
            }
        }

        [Fact(DisplayName = nameof(CacheSearchLanguageMismatchFilterTest))]
        public async Task CacheSearchLanguageMismatchFilterTest()
        {
            TemplateDiscoveryMetadata mockTemplateDiscoveryMetadata = SetupDiscoveryMetadata(false);
            MockCliNuGetMetadataSearchSource.SetupMockData(mockTemplateDiscoveryMetadata);
            EngineEnvironmentSettings.SettingsLoader.Components.Register(typeof(MockCliNuGetMetadataSearchSource));

            MockNewCommandInput commandInput = new MockNewCommandInput("bar", "VB");

            TemplateSearchCoordinator searchCoordinator = CliTemplateSearchCoordinatorFactory.CreateCliTemplateSearchCoordinator(EngineEnvironmentSettings, commandInput, DefaultLanguage);
            SearchResults searchResults = await searchCoordinator.SearchAsync();

            Assert.True(searchResults.AnySources);
            Assert.Equal(0, searchResults.MatchesBySource.Count);
        }

        private static readonly PackInfo _packOneInfo = new PackInfo("PackOne", "1.0.0");
        private static readonly PackInfo _packTwoInfo = new PackInfo("PackTwo", "1.6.0");
        private static readonly PackInfo _packThreeInfo = new PackInfo("PackThree", "2.1");

        private static readonly ITemplateInfo _fooOneTemplate =
            new MockTemplateInfo("foo1", name: "MockFooTemplateOne", identity: "Mock.Foo.1", groupIdentity: "Mock.Foo", author: "TestAuthor")
                .WithDescription("Mock Foo template one")
                .WithTag("Framework", "netcoreapp3.0", "netcoreapp3.1")
                .WithTag("language", "C#")
                .WithTag("type", "project");

        private static readonly ITemplateInfo _fooTwoTemplate =
            new MockTemplateInfo("foo2", name: "MockFooTemplateTwo", identity: "Mock.Foo.2", groupIdentity: "Mock.Foo")
                .WithDescription("Mock Foo template two")
                .WithTag("Framework", "netcoreapp2.0", "netcoreapp2.1", "netcoreapp3.1")
                .WithTag("language", "C#");


        private static readonly ITemplateInfo _barCSharpTemplate =
            new MockTemplateInfo("barC", name: "MockBarCsharpTemplate", identity: "Mock.Bar.1.Csharp", groupIdentity: "Mock.Bar")
                .WithDescription("Mock Bar CSharp template")
                .WithTag("language", "C#");

        private static readonly ITemplateInfo _barFSharpTemplate =
            new MockTemplateInfo("barF", name: "MockBarFSharpTemplate", identity: "Mock.Bar.1.FSharp", groupIdentity: "Mock.Bar")
                .WithDescription("Mock Bar FSharp template")
                .WithTag("language", "F#");

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
