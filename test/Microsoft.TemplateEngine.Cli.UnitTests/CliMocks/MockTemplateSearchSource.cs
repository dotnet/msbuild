// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateSearch.Common;

namespace Microsoft.TemplateEngine.Cli.UnitTests.CliMocks
{
    public class MockTemplateSearchSource : ITemplateSearchSource
    {
        private static IDictionary<Guid, IReadOnlyList<ITemplateNameSearchResult>> _resultsById;

        private readonly Guid _id;

        private ISearchPackFilter _packFilter;

        static MockTemplateSearchSource()
        {
            ClearResultsForAllSources();
        }

        public MockTemplateSearchSource()
        {
            _id = Guid.NewGuid();
            DisplayName = string.Format("Mock Search Source {0}", _id);
        }

        public string DisplayName { get; set; }

        public Guid Id => _id;

        public static IReadOnlyDictionary<string, Guid> SetupMultipleSources(IEngineEnvironmentSettings environmentSettings, IReadOnlyDictionary<string, IReadOnlyList<ITemplateNameSearchResult>> dataForSources)
        {
            // create the sources initially
            for (int i = 0; i < dataForSources.Count; i++)
            {
                environmentSettings.SettingsLoader.Components.Register(typeof(MockTemplateSearchSource));
            }

            // assign the source data to the sources, mapping the source to its id
            Dictionary<string, Guid> sourceNameToIdMap = new Dictionary<string, Guid>();
            IList<string> sourceNameOrder = dataForSources.Keys.ToList();
            int sourceIndex = 0;

            foreach (ITemplateSearchSource searchSource in environmentSettings.SettingsLoader.Components.OfType<ITemplateSearchSource>())
            {
                if (searchSource is MockTemplateSearchSource mockSource)
                {
                    mockSource.DisplayName = sourceNameOrder[sourceIndex];  // reset the auto-assigned name to the input name.
                    SetPossibleResultsForId(mockSource.Id, dataForSources[sourceNameOrder[sourceIndex]]);
                    sourceNameToIdMap[mockSource.DisplayName] = mockSource.Id;
                    sourceIndex++;
                }
            }

            return sourceNameToIdMap;
        }

        public static void SetPossibleResultsForId(Guid id, IReadOnlyList<ITemplateNameSearchResult> possibleResults)
        {
            _resultsById[id] = possibleResults;
        }

        public static void ClearResultsForSourceId(Guid id)
        {
            _resultsById.Remove(id);
        }

        public static void ClearResultsForAllSources()
        {
            _resultsById = new Dictionary<Guid, IReadOnlyList<ITemplateNameSearchResult>>();
        }

        public Task<bool> TryConfigure(IEngineEnvironmentSettings environment, IReadOnlyList<IManagedTemplatePackage> existingTemplatePackage)
        {
            _packFilter = new NupkgHigherVersionInstalledPackFilter(existingTemplatePackage);

            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<ITemplateNameSearchResult>> CheckForTemplateNameMatchesAsync(string templateName)
        {
            List<ITemplateNameSearchResult> matches = new List<ITemplateNameSearchResult>();

            IReadOnlyList<ITemplateNameSearchResult> possibleSearchResults = GetPossibleResultsOrDefaultForId(_id);

            foreach (ITemplateNameSearchResult candidate in possibleSearchResults)
            {
                if (candidate.Template.Name.Contains(templateName) || candidate.Template.ShortNameList.Any(shortName => shortName.Contains(templateName)))
                {
                    if (!_packFilter.ShouldPackBeFiltered(candidate.Template.Name, candidate.PackInfo.Version))
                    {
                        matches.Add(candidate);
                    }
                }
            }

            IReadOnlyList<ITemplateNameSearchResult> returnResults = matches;
            return Task.FromResult(returnResults);
        }

        public Task<IReadOnlyDictionary<string, PackToTemplateEntry>> CheckForTemplatePackMatchesAsync(IReadOnlyList<string> packNameList)
        {
            throw new NotImplementedException();
        }

        private static IReadOnlyList<ITemplateNameSearchResult> GetPossibleResultsOrDefaultForId(Guid id)
        {
            if (_resultsById.TryGetValue(id, out IReadOnlyList<ITemplateNameSearchResult> possibleResults))
            {
                return possibleResults;
            }

            return new List<ITemplateNameSearchResult>();
        }
    }
}
