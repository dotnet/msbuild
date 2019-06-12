using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;
using Microsoft.TemplateEngine.Cli.TemplateUpdater;

namespace Microsoft.TemplateEngine.Cli.TemplateUpdate
{
    public class TemplateUpdateCoordinator
    {
        public TemplateUpdateCoordinator(IEngineEnvironmentSettings environmentSettings, IInstaller installer, string commandName)
        {
            _environmentSettings = environmentSettings;
            _installer = installer;
            _commandName = commandName;
            _updateChecker = new TemplateUpdateChecker(_environmentSettings);
        }

        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly IInstaller _installer;
        private readonly TemplateUpdateChecker _updateChecker;
        private readonly string _commandName;

        public async Task<bool> CheckForUpdates(IReadOnlyList<IInstallUnitDescriptor> installUnitsToCheck, bool applyUpdates)
        {
            try
            {
                IReadOnlyList<IUpdateUnitDescriptor> updateDescriptorList = await _updateChecker.CheckForUpdatesAsync(installUnitsToCheck);

                if (updateDescriptorList.Count == 0)
                {
                    DisplayNoUpdatesFoundMessage();
                    return true;
                }

                if (applyUpdates)
                {
                    bool anyUpdateErrors = ApplyUpdates(updateDescriptorList);
                    return !anyUpdateErrors;    // no errors, return true for success
                }
                else
                {
                    ListUpdates(updateDescriptorList);
                    return true;
                }
            }
            catch
            {
                return false;
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
                DisplayUpdateInfoForPack(updateDescriptor);
            }
        }

        private void DisplayUpdateInfoForPack(IUpdateUnitDescriptor updateDescriptor)
        {
            Reporter.Output.WriteLine(string.Format(LocalizableStrings.UpdateAvailable, updateDescriptor.UpdateDisplayInfo));
            Reporter.Output.WriteLine(string.Format(LocalizableStrings.UpdateCheck_InstallCommand, _commandName, updateDescriptor.InstallString));
        }

        private bool ApplyUpdates(IReadOnlyList<IUpdateUnitDescriptor> updateDescriptorList)
        {
            bool anyErrors = false;

            foreach (IUpdateUnitDescriptor updateDescriptor in updateDescriptorList)
            {
                if (_updateChecker.TryGetUpdaterForDescriptorFactoryId(updateDescriptor.InstallUnitDescriptor.FactoryId, out IUpdater updater))
                {
                    try
                    {
                        DisplayUpdateInfoForPack(updateDescriptor);
                        Reporter.Output.WriteLine(LocalizableStrings.UpdateApplyStartMessage.Bold().Red());
                        updater.ApplyUpdates(_installer, new List<IUpdateUnitDescriptor>() { updateDescriptor });
                        Reporter.Output.WriteLine(LocalizableStrings.UpdateApplySuccessMessage.Bold().Red());
                        Reporter.Output.WriteLine();
                    }
                    catch
                    {
                        Reporter.Error.WriteLine(string.Format(LocalizableStrings.TemplateUpdateError, updateDescriptor.InstallUnitDescriptor.Identifier).Bold().Red());
                        anyErrors = true;
                    }
                }
            }

            return anyErrors;
        }
    }
}
