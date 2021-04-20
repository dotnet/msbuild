// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Cli.UnitTests.CliMocks;
using Microsoft.TemplateEngine.Edge.Installers.NuGet;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateSearch.Common;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class TemplateSearcherTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private IEngineEnvironmentSettings _engineEnvironmentSettings;
        public TemplateSearcherTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
        }

        private static readonly PackInfo _fooPackInfo = new PackInfo("fooPack", "1.0.0");

        private static readonly PackInfo _barPackInfo = new PackInfo("barPack", "2.0.0");

        private static readonly PackInfo _redPackInfo = new PackInfo("redPack", "1.1");

        private static readonly PackInfo _bluePackInfo = new PackInfo("bluePack", "2.1");

        private static readonly PackInfo _greenPackInfo = new PackInfo("greenPack", "3.0.0");

        [Fact(DisplayName = nameof(TwoSourcesAreBothSearched))]
        public void TwoSourcesAreBothSearched()
        {
            _engineEnvironmentSettings.SettingsLoader.Components.Register(typeof(MockTemplateSearchSource));
            _engineEnvironmentSettings.SettingsLoader.Components.Register(typeof(MockTemplateSearchSource));

            IList<ITemplateSearchSource> searchSources = _engineEnvironmentSettings.SettingsLoader.Components.OfType<ITemplateSearchSource>().ToList();

            Assert.Equal(2, searchSources.Count);
        }

        [Fact(DisplayName = nameof(SourcesCorrectlySearchOnName))]
        public void SourcesCorrectlySearchOnName()
        {
            MockTemplateSearchSource.ClearResultsForAllSources();
            IReadOnlyDictionary<string, Guid> sourceNameToIdMap = MockTemplateSearchSource.SetupMultipleSources(_engineEnvironmentSettings, GetMockNameSearchResults());

            const string templateName = "foo";

            TemplateSearcher searcher = new TemplateSearcher(_engineEnvironmentSettings, "C#", MockTemplateSearchHelpers.DefaultMatchFilter);
            List<IManagedTemplatePackage> existingInstalls = new List<IManagedTemplatePackage>();
            SearchResults searchResults = searcher.SearchForTemplatesAsync(existingInstalls, templateName).Result;
            Assert.True(searchResults.AnySources);
            Assert.Equal(1, searchResults.MatchesBySource.Count);
            Assert.Equal("source one", searchResults.MatchesBySource[0].SourceDisplayName);
            Assert.Equal(1, searchResults.MatchesBySource[0].PacksWithMatches.Count);
            Assert.True(searchResults.MatchesBySource[0].PacksWithMatches.ContainsKey(_fooPackInfo));

            Assert.Single(searchResults.MatchesBySource[0].PacksWithMatches[_fooPackInfo].TemplateMatches.Where(x => string.Equals(x.Info.Name, "MockFooTemplateOne")));
            Assert.Single(searchResults.MatchesBySource[0].PacksWithMatches[_fooPackInfo].TemplateMatches.Where(x => string.Equals(x.Info.Name, "MockFooTemplateTwo")));
        }

        [Fact(DisplayName = nameof(SearcherCorrectlyFiltersSpecifiedPack))]
        public void SearcherCorrectlyFiltersSpecifiedPack()
        {
            const string templateName = "foo";

            TemplateSearcher searcher = new TemplateSearcher(_engineEnvironmentSettings, "C#", MockTemplateSearchHelpers.DefaultMatchFilter);

            IReadOnlyList<IManagedTemplatePackage> packsToIgnore = new List<IManagedTemplatePackage>()
            {
                new MockManagedTemplatePackage()
            };

            SearchResults searchResults = searcher.SearchForTemplatesAsync(packsToIgnore, templateName).Result;
            Assert.Equal(0, searchResults.MatchesBySource.Count);
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<ITemplateNameSearchResult>> GetMockNameSearchResults()
        {
            Dictionary<string, IReadOnlyList<ITemplateNameSearchResult>> dataForSources = new Dictionary<string, IReadOnlyList<ITemplateNameSearchResult>>();

            List<TemplateNameSearchResult> sourceOneResults = new List<TemplateNameSearchResult>();

            ITemplateInfo sourceOneTemplateOne = new MockTemplateInfo("foo1", name: "MockFooTemplateOne", identity: "Mock.Foo.1").WithDescription("Mock Foo template one");
            TemplateNameSearchResult sourceOneResultOne = new TemplateNameSearchResult(sourceOneTemplateOne, _fooPackInfo);
            sourceOneResults.Add(sourceOneResultOne);

            ITemplateInfo sourceOneTemplateTwo = new MockTemplateInfo("foo2", name: "MockFooTemplateTwo", identity: "Mock.Foo.2").WithDescription("Mock Foo template two");
            TemplateNameSearchResult sourceOneResultTwo = new TemplateNameSearchResult(sourceOneTemplateTwo, _fooPackInfo);
            sourceOneResults.Add(sourceOneResultTwo);

            ITemplateInfo sourceOneTemplateThree = new MockTemplateInfo("bar1", name: "MockBarTemplateOne", identity: "Mock.Bar.1").WithDescription("Mock Bar template one");
            TemplateNameSearchResult sourceOneResultThree = new TemplateNameSearchResult(sourceOneTemplateThree, _barPackInfo);
            sourceOneResults.Add(sourceOneResultThree);

            dataForSources["source one"] = sourceOneResults;

            List<TemplateNameSearchResult> sourceTwoResults = new List<TemplateNameSearchResult>();

            ITemplateInfo sourceTwoTemplateOne = new MockTemplateInfo("red", name: "MockRedTemplate", identity: "Mock.Red.1").WithDescription("Mock red template");

            TemplateNameSearchResult sourceTwoResultOne = new TemplateNameSearchResult(sourceTwoTemplateOne, _redPackInfo);
            sourceTwoResults.Add(sourceTwoResultOne);

            ITemplateInfo sourceTwoTemplateTwo = new MockTemplateInfo("blue", name: "MockBlueTemplate", identity: "Mock.Blue.1").WithDescription("Mock blue template");
            TemplateNameSearchResult sourceTwoResultTwo = new TemplateNameSearchResult(sourceTwoTemplateTwo, _bluePackInfo);
            sourceTwoResults.Add(sourceTwoResultTwo);

            ITemplateInfo sourceTwoTemplateThree = new MockTemplateInfo("green", name: "MockGreenTemplate", identity: "Mock.Green.1").WithDescription("Mock green template");
            TemplateNameSearchResult sourceTwoResultThree = new TemplateNameSearchResult(sourceTwoTemplateThree, _greenPackInfo);
            sourceTwoResults.Add(sourceTwoResultThree);

            dataForSources["source two"] = sourceTwoResults;

            return dataForSources;
        }
    }
}
