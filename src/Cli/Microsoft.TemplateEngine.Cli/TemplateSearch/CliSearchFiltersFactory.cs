// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateSearch.Common;

namespace Microsoft.TemplateEngine.Cli.TemplateSearch
{
    internal class CliSearchFiltersFactory
    {
        private IReadOnlyDictionary<string, string?> _existingTemplatePackageFilterData;

        internal CliSearchFiltersFactory(IReadOnlyList<IManagedTemplatePackage> installedPackages)
        {
            Dictionary<string, string?> filterData = new Dictionary<string, string?>();
            foreach (IManagedTemplatePackage descriptor in installedPackages)
            {
                filterData[descriptor.Identifier] = descriptor.Version;
            }
            _existingTemplatePackageFilterData = filterData;
        }

        private static Func<TemplateSearchData, bool> IsNotHiddenBySearchFile =>
            (templateSearchData) =>
            {
                if (templateSearchData.AdditionalData.TryGetValue(CliHostSearchCacheData.DataName, out object? hostDataRaw)
                    && hostDataRaw is HostSpecificTemplateData hostData)
                {
                    return !hostData.IsHidden;
                }
                return true;
            };

        private Func<TemplatePackageSearchData, bool> AlreadyInstalledFilter =>
            (templatePackageSearchData) =>
            {
                if (!_existingTemplatePackageFilterData!.TryGetValue(templatePackageSearchData.Name, out string? existingPackVersion))
                {
                    // no existing install of this pack - don't filter it
                    return true;
                }
                return existingPackVersion != templatePackageSearchData.Version;
            };

        internal static Func<TemplatePackageSearchData, IReadOnlyList<ITemplateInfo>> GetMatchingTemplatesFilter(SearchCommandArgs commandArgs)
        {
            return (templatePackageSearchData) =>
            {
                InMemoryHostSpecificDataLoader hostDataLoader = new InMemoryHostSpecificDataLoader(templatePackageSearchData);
                IEnumerable<TemplateSearchData> templates = templatePackageSearchData.Templates.Where(template => IsNotHiddenBySearchFile(template));
                IEnumerable<TemplateGroup> templateGroups = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templates, hostDataLoader));
                IEnumerable<Func<TemplateGroup, MatchInfo?>> groupFilters = new[]
                {
                    CliFilters.NameTemplateGroupFilter(commandArgs.SearchNameCriteria)
                };

                IEnumerable<Func<ITemplateInfo, MatchInfo?>> templateFilters =
                    commandArgs.AppliedFilters
                        .OfType<TemplateFilterOptionDefinition>()
                        .Select(filter => filter.TemplateMatchFilter(commandArgs.GetFilterValue(filter)));

                IEnumerable<TemplateGroupMatchInfo> matchInformation =
                    templateGroups.Select(
                        group =>
                            TemplateGroupMatchInfo.ApplyFilters(
                                group,
                                groupFilters,
                                templateFilters,
                                //TODO: implement it for template options matching, for now the filter just returns empty collection.
                                CliFilters.EmptyTemplateParameterFilter()));

                return matchInformation
                    .Where(group => group.IsGroupAndTemplateInfoAndParametersMatch)
                    .SelectMany(group => group.TemplatesWithMatchingParameters)
                    .ToList();
            };
        }

        internal Func<TemplatePackageSearchData, bool> GetPackFilter(SearchCommandArgs commandArgs)
        {
            return templatePackageSearchData =>
            {
                return AlreadyInstalledFilter(templatePackageSearchData)
                && commandArgs.AppliedFilters
                        .OfType<PackageFilterOptionDefinition>()
                        .All(filter => filter.PackageMatchFilter(commandArgs.GetFilterValue(filter))(templatePackageSearchData));
            };
        }

        private class InMemoryHostSpecificDataLoader : IHostSpecificDataLoader
        {
            private IReadOnlyDictionary<string, HostSpecificTemplateData> _hostSpecificData;

            internal InMemoryHostSpecificDataLoader(TemplatePackageSearchData templatePackageSearchData)
            {
                Dictionary<string, HostSpecificTemplateData> hostSpecificData = new Dictionary<string, HostSpecificTemplateData>();
                foreach (var templateData in templatePackageSearchData.Templates)
                {
                    if (templateData.AdditionalData.TryGetValue(CliHostSearchCacheData.DataName, out object? hostDataRaw)
                        && hostDataRaw is HostSpecificTemplateData hostData)
                    {
                        hostSpecificData[((ITemplateInfo)templateData).Identity] = hostData;
                    }
                }
                _hostSpecificData = hostSpecificData;
            }

            public HostSpecificTemplateData ReadHostSpecificTemplateData(ITemplateInfo templateInfo)
            {
                if (_hostSpecificData.TryGetValue(templateInfo.Identity, out HostSpecificTemplateData? data))
                {
                    return data;
                }

                return HostSpecificTemplateData.Default;
            }
        }
    }
}
