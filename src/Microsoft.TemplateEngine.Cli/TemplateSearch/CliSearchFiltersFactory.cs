// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateSearch.Common;

namespace Microsoft.TemplateEngine.Cli.TemplateSearch
{
    internal class CliSearchFiltersFactory
    {
        private IReadOnlyDictionary<string, string> _existingTemplatePackageFilterData;

        internal CliSearchFiltersFactory(IReadOnlyList<IManagedTemplatePackage> installedPackages)
        {
            Dictionary<string, string> filterData = new Dictionary<string, string>();
            foreach (IManagedTemplatePackage descriptor in installedPackages)
            {
                filterData[descriptor.Identifier] = descriptor.Version;
            }
            _existingTemplatePackageFilterData = filterData;
        }

        private static Func<TemplateSearchData, bool> IsNotHiddenBySearchFile =>
            (templateSearchData) =>
            {
                if (templateSearchData.AdditionalData.TryGetValue(CliTemplateSearchCoordinatorFactory.CliHostDataName, out object? hostDataRaw)
                    && hostDataRaw is HostSpecificTemplateData hostData)
                {
                    return !hostData.IsHidden;
                }
                return true;
            };

        private Func<TemplatePackageSearchData, bool> AlreadyInstalledFilter =>
            (templatePackageSearchData) =>
            {
                if (!_existingTemplatePackageFilterData!.TryGetValue(templatePackageSearchData.PackageInfo.Name, out string? existingPackVersion))
                {
                    // no existing install of this pack - don't filter it
                    return true;
                }
                return existingPackVersion != templatePackageSearchData.PackageInfo.Version;
            };

        internal static Func<TemplatePackageSearchData, IReadOnlyList<ITemplateInfo>> GetMatchingTemplatesFilter (INewCommandInput commandInput)
        {
            return (templatePackageSearchData) =>
            {
                InMemoryHostSpecificDataLoader hostDataLoader = new InMemoryHostSpecificDataLoader(templatePackageSearchData);
                IEnumerable<TemplateSearchData> templates = templatePackageSearchData.Templates.Where(template => IsNotHiddenBySearchFile(template));
                IEnumerable<TemplateGroup> templateGroups = TemplateGroup.FromTemplateList(templates.Select(t => t.TemplateInfo));
                IEnumerable<Func<TemplateGroup, MatchInfo?>> groupFilters = new[]
                {
                    CliFilters.NameTemplateGroupFilter(commandInput.SearchNameCriteria)
                };

                IEnumerable<Func<ITemplateInfo, MatchInfo?>> templateFilters =
                    CliTemplateSearchCoordinator.SupportedFilters
                        .OfType<TemplateFilterOption>()
                        .Select(filter => filter.TemplateMatchFilter(commandInput));

                IEnumerable<TemplateGroupMatchInfo> matchInformation =
                    templateGroups.Select(
                        group =>
                            TemplateGroupMatchInfo.ApplyFilters(
                                group,
                                groupFilters,
                                templateFilters,
                                CliFilters.ListTemplateParameterFilter(hostDataLoader, commandInput)));
                return matchInformation
                    .Where(group => group.IsGroupAndTemplateInfoAndParametersMatch)
                    .SelectMany(group => group.TemplatesWithMatchingParameters)
                    .ToList();
            };
        }

        internal Func<TemplatePackageSearchData, bool> GetPackFilter(INewCommandInput commandInput)
        {
            return templatePackageSearchData =>
            {
                return AlreadyInstalledFilter(templatePackageSearchData)
                && CliTemplateSearchCoordinator.SupportedFilters
                        .OfType<PackageFilterOption>()
                        .Select(filter => filter.PackageMatchFilter)
                        .All(packFilter => packFilter(commandInput)(templatePackageSearchData.PackageInfo));
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
                    if (templateData.AdditionalData.TryGetValue(CliTemplateSearchCoordinatorFactory.CliHostDataName, out object? hostDataRaw)
                        && hostDataRaw is HostSpecificTemplateData hostData)
                    {
                        hostSpecificData[templateData.TemplateInfo.Identity] = hostData;
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
