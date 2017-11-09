// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;

namespace Microsoft.TemplateEngine.Cli
{
    public class TemplateUpdateChecker
    {
        public TemplateUpdateChecker(IEngineEnvironmentSettings environmentSettings)
        {
            _environmentSettings = environmentSettings;
        }

        private readonly IEngineEnvironmentSettings _environmentSettings;
        // Maps the install unit descriptor factory ids to the corresponding updaters.
        private Dictionary<Guid, IUpdater> _factoryIdToUpdaterMap;

        public async Task<IReadOnlyList<IUpdateUnitDescriptor>> CheckForUpdatesAsync(IReadOnlyList<IInstallUnitDescriptor> installUnitsToCheck)
        {
            EnsureFactoryToUpdaterMapping();
            Dictionary<Guid, List<IInstallUnitDescriptor>> installUnitsToCheckForUpdates = new Dictionary<Guid, List<IInstallUnitDescriptor>>();
            List<IInstallUnitDescriptor> descriptorsWithoutUpdaters = new List<IInstallUnitDescriptor>();
            List<IUpdateUnitDescriptor> updateDescriptors = new List<IUpdateUnitDescriptor>();

            // collect the descriptors by their factoryId, ignoring descriptors that don't have corresponding factories or updaters.
            foreach (IInstallUnitDescriptor descriptor in installUnitsToCheck)
            {
                if (_factoryIdToUpdaterMap.TryGetValue(descriptor.FactoryId, out IUpdater updater))
                {
                    if (!installUnitsToCheckForUpdates.TryGetValue(descriptor.FactoryId, out List<IInstallUnitDescriptor> updateList))
                    {
                        updateList = new List<IInstallUnitDescriptor>();
                        installUnitsToCheckForUpdates[descriptor.FactoryId] = updateList;
                    }

                    updateList.Add(descriptor);
                }
                else
                {
                    descriptorsWithoutUpdaters.Add(descriptor);
                }
            }

            if (descriptorsWithoutUpdaters.Count > 0)
            {
                Reporter.Output.WriteLine(LocalizableStrings.UpdateCheckerNotAvailable.Bold().Red());
                foreach (IInstallUnitDescriptor descriptor in descriptorsWithoutUpdaters)
                {
                    Reporter.Output.WriteLine($"\t{descriptor.UserReadableIdentifier}".Bold().Red());
                }
            }

            // check for updates
            foreach (KeyValuePair<Guid, List<IInstallUnitDescriptor>> descriptorsForType in installUnitsToCheckForUpdates)
            {
                if (_factoryIdToUpdaterMap.TryGetValue(descriptorsForType.Key, out IUpdater updater))
                {
                    IReadOnlyList<IUpdateUnitDescriptor> updatesForType = null;

                    try
                    {
                        updatesForType = await updater.CheckForUpdatesAsync(descriptorsForType.Value);
                    }
                    catch
                    {
                        Reporter.Error.WriteLine(string.Format(LocalizableStrings.UpdateCheckError.Bold().Red(), updater.DisplayIdentifier));
                    }

                    if (updatesForType != null && updatesForType.Count > 0)
                    {
                        updateDescriptors.AddRange(updatesForType);
                    }
                }
            }

            return updateDescriptors;
        }

        public bool TryGetUpdaterForDescriptorFactoryId(Guid factoryId, out IUpdater updater)
        {
            EnsureFactoryToUpdaterMapping();

            return _factoryIdToUpdaterMap.TryGetValue(factoryId, out updater);
        }

        private void EnsureFactoryToUpdaterMapping()
        {
            if (_factoryIdToUpdaterMap == null)
            {
                Dictionary<Guid, IUpdater> factoryIdToUpdaterMap = new Dictionary<Guid, IUpdater>();

                foreach (IUpdater updater in _environmentSettings.SettingsLoader.Components.OfType<IUpdater>().ToList())
                {
                    factoryIdToUpdaterMap[updater.DescriptorFactoryId] = updater;
                }

                _factoryIdToUpdaterMap = factoryIdToUpdaterMap;
            }
        }
    }
}
