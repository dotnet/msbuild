// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;

namespace Microsoft.TemplateEngine.Cli
{
    public class TemplateUpdateCoordinator
    {
        private enum ApplyUpdatesChoice
        {
            All,
            None,
            Prompt,
            InvalidChoice
        };

        public TemplateUpdateCoordinator(IEngineEnvironmentSettings environmentSettings, IInstaller installer)
        {
            _environmentSettings = environmentSettings;
            _installer = installer;
            _updateChecker = new TemplateUpdateChecker(_environmentSettings);
        }

        private IEngineEnvironmentSettings _environmentSettings;
        private IInstaller _installer;
        private TemplateUpdateChecker _updateChecker;

        public async void UpdateTemplates(IReadOnlyList<IInstallUnitDescriptor> installUnitsToCheck, Func<string> inputGetter, bool applyAll = false)
        {
            IReadOnlyList<IUpdateUnitDescriptor> updateDescriptorList = await _updateChecker.CheckForUpdatesAsync(installUnitsToCheck);

            if (updateDescriptorList.Count == 0)
            {
                DisplayNoUpdatesFoundMessage();
                return;
            }

            ListUpdates(updateDescriptorList);
            ApplyUpdatesChoice allUpdatesChoice;
            if (applyAll)
            {
                allUpdatesChoice = ApplyUpdatesChoice.All;
            }
            else
            {
                allUpdatesChoice = GetUserChoiceForAllUpdates(inputGetter);
            }

            switch (allUpdatesChoice)
            {
                case ApplyUpdatesChoice.None:
                    return;
                case ApplyUpdatesChoice.All:
                    ApplyUpdates(updateDescriptorList);
                    break;
                case ApplyUpdatesChoice.Prompt:
                    foreach (IUpdateUnitDescriptor descriptor in updateDescriptorList)
                    {
                        bool shouldUpdate = GetUserChoiceForIndividualUpdate(descriptor, inputGetter);
                        if (shouldUpdate)
                        {
                            ApplyUpdates(new List<IUpdateUnitDescriptor>() { descriptor });
                        }
                    }
                    break;
            }
        }

        private void ApplyUpdates(IReadOnlyList<IUpdateUnitDescriptor> updateDescriptorList)
        {
            foreach (IUpdateUnitDescriptor descriptor in updateDescriptorList)
            {
                if (_updateChecker.TryGetUpdaterForDescriptorFactoryId(descriptor.InstallUnitDescriptor.FactoryId, out IUpdater updater))
                {
                    try
                    {
                        updater.ApplyUpdates(_installer, new List<IUpdateUnitDescriptor>() { descriptor });
                    }
                    catch
                    {
                        Reporter.Error.WriteLine(string.Format(LocalizableStrings.TemplateUpdateError, descriptor.InstallUnitDescriptor.UserReadableIdentifier).Bold().Red());
                    }
                }
            }
        }

        private void DisplayNoUpdatesFoundMessage()
        {
            Reporter.Output.WriteLine(LocalizableStrings.NoUpdates.Bold().Red());
            Reporter.Output.WriteLine();
        }

        private void ListUpdates(IReadOnlyList<IUpdateUnitDescriptor> updateDescriptorList)
        {
            Reporter.Output.WriteLine(LocalizableStrings.UpdatesAvailableListHeader.Bold().Red());

            foreach (IUpdateUnitDescriptor updateDescriptor in updateDescriptorList)
            {
                Reporter.Output.WriteLine($"\t{updateDescriptor.UpdateDisplayInfo}");
            }
        }

        private ApplyUpdatesChoice GetUserChoiceForAllUpdates(Func<string> inputGetter)
        {
            ApplyUpdatesChoice choice = ApplyUpdatesChoice.InvalidChoice;

            do
            {
                Reporter.Output.WriteLine(LocalizableStrings.AllUpdatesApplyPrompt.Bold().Red());
                string userChoice = inputGetter();

                switch (userChoice.ToLowerInvariant())
                {
                    case "a":
                    case "all":
                        choice = ApplyUpdatesChoice.All;
                        break;
                    case "n":
                    case "none":
                        choice = ApplyUpdatesChoice.None;
                        break;
                    case "p":
                    case "prompt":
                        choice = ApplyUpdatesChoice.Prompt;
                        break;
                    default:
                        choice = ApplyUpdatesChoice.InvalidChoice;
                        Reporter.Output.Write(LocalizableStrings.ApplyUpdatesInvalidChoiceResponse.Bold().Red());
                        break;
                }
            } while (choice == ApplyUpdatesChoice.InvalidChoice);

            return choice;
        }

        private bool GetUserChoiceForIndividualUpdate(IUpdateUnitDescriptor descriptor, Func<string> inputGetter)
        {
            bool installChoice = false;
            bool validChoice = false;

            do
            {
                Reporter.Output.WriteLine(string.Format(LocalizableStrings.SingleUpdateApplyPrompt.Bold().Red(), descriptor.UpdateDisplayInfo));

                string userChoice = inputGetter();

                switch (userChoice.ToLowerInvariant())
                {
                    case "y":
                    case "yes":
                        installChoice = true;
                        validChoice = true;
                        break;
                    case "n":
                    case "no":
                        installChoice = false;
                        validChoice = true;
                        break;
                    default:
                        Reporter.Output.WriteLine(LocalizableStrings.ApplyUpdatesInvalidChoiceResponse.Bold().Red());
                        break;
                }
            } while (!validChoice);

            return installChoice;
        }
    }
}
