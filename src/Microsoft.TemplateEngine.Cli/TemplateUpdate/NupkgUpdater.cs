// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;
using Microsoft.TemplateEngine.Edge.TemplateUpdates;
using Microsoft.TemplateSearch.Common;

namespace Microsoft.TemplateEngine.Cli.TemplateUpdater
{
    internal class NupkgUpdater : IUpdater
    {
        public Guid Id { get; } = new Guid("DB5BF8D8-6181-496A-97DA-58616E135701");

        public Guid DescriptorFactoryId { get; } = NupkgInstallUnitDescriptorFactory.FactoryId;

        public string DisplayIdentifier { get; } = "Nupkg";
        private IEngineEnvironmentSettings _environmentSettings;
        private IReadOnlyList<IInstallUnitDescriptor> _existingInstallDescriptors;

        private bool _isInitialized = false;
        private IReadOnlyList<ITemplateSearchSource> _templateSearchSourceList;

        public void Configure(IEngineEnvironmentSettings environmentSettings, IReadOnlyList<IInstallUnitDescriptor> existingInstallDescriptors)
        {
            _environmentSettings = environmentSettings;
            _existingInstallDescriptors = existingInstallDescriptors;
        }

        private async Task EnsureInitializedAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            List<ITemplateSearchSource> searchSourceList = new List<ITemplateSearchSource>();

            foreach (ITemplateSearchSource searchSource in _environmentSettings.SettingsLoader.Components.OfType<ITemplateSearchSource>())
            {
                try
                {
                    if (await searchSource.TryConfigure(_environmentSettings, _existingInstallDescriptors))
                    {
                        searchSourceList.Add(searchSource);
                    }
                }
                catch (Exception ex)
                {
                    Reporter.Error.WriteLine($"Error configuring search source: {searchSource.DisplayName}.\r\nError = {ex.Message}");
                }
            }

            _templateSearchSourceList = searchSourceList;

            _isInitialized = true;
        }

        public async Task<IReadOnlyList<IUpdateUnitDescriptor>> CheckForUpdatesAsync(IReadOnlyList<IInstallUnitDescriptor> descriptorsToCheck)
        {
            await EnsureInitializedAsync();

            IReadOnlyDictionary<string, IInstallUnitDescriptor> installedPackToInstallDescriptorMap = descriptorsToCheck.ToDictionary(d => d.Identifier, d => d);

            List<IUpdateUnitDescriptor> updateList = new List<IUpdateUnitDescriptor>();

            foreach (ITemplateSearchSource searchSource in _templateSearchSourceList)
            {
                IReadOnlyDictionary<string, PackToTemplateEntry> candidateUpdatePackMatchList = await searchSource.CheckForTemplatePackMatchesAsync(installedPackToInstallDescriptorMap.Keys.ToList());

                foreach (KeyValuePair<string, PackToTemplateEntry> candidateUpdatePackMatch in candidateUpdatePackMatchList)
                {
                    string packName = candidateUpdatePackMatch.Key;

                    if (installedPackToInstallDescriptorMap.TryGetValue(packName, out IInstallUnitDescriptor installDescriptor)
                            && (installDescriptor is NupkgInstallUnitDescriptor nupkgInstallDescriptor))
                    {
                        string installString = $"{packName}::{candidateUpdatePackMatch.Value.Version}";
                        string displayString = string.Format(LocalizableStrings.NuGetPackUpdateDisplayInfo, packName, nupkgInstallDescriptor.Version);
                        IUpdateUnitDescriptor updateDescriptor = new UpdateUnitDescriptor(installDescriptor, installString, displayString);
                        updateList.Add(updateDescriptor);
                    }
                }
            }

            return updateList;
        }

        public void ApplyUpdates(IInstaller installer, IReadOnlyList<IUpdateUnitDescriptor> updatesToApply)
        {
            IReadOnlyList<IUpdateUnitDescriptor> filteredUpdateToApply = updatesToApply.Where(x => x.InstallUnitDescriptor.FactoryId == DescriptorFactoryId).ToList();
            installer.InstallPackages(filteredUpdateToApply.Select(x => x.InstallString));
        }
    }
}
