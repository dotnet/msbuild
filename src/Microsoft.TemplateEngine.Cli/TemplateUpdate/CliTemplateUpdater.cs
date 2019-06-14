using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;
using Microsoft.TemplateSearch.Common.TemplateUpdate;

namespace Microsoft.TemplateEngine.Cli.TemplateUpdate
{
    internal class CliTemplateUpdater
    {
        public CliTemplateUpdater(IEngineEnvironmentSettings environmentSettings, IInstallerBase installer, string commandName)
        {
            _commandName = commandName;
            _updateCoordinator = new TemplateUpdateCoordinator(environmentSettings, installer);
        }

        private readonly string _commandName;
        private readonly TemplateUpdateCoordinator _updateCoordinator;

        public async Task<bool> CheckForUpdatesAsync(IReadOnlyList<IInstallUnitDescriptor> installUnitsToCheck, bool applyUpdates)
        {
            try
            {
                IUpdateCheckResult updateCheckResult = await _updateCoordinator.CheckForUpdatesAsync(installUnitsToCheck);

                if (updateCheckResult.Updates.Count == 0)
                {
                    DisplayNoUpdatesFoundMessage();
                    return true;
                }

                if (applyUpdates)
                {
                    return TryApplyUpdates(updateCheckResult.Updates);
                }
                else
                {
                    ListUpdates(updateCheckResult.Updates);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private bool TryApplyUpdates(IReadOnlyList<IUpdateUnitDescriptor> updatesToAttempt)
        {
            bool anyUpdateFailures = false;

            foreach (IUpdateUnitDescriptor updateDescriptor in updatesToAttempt)
            {
                DisplayUpdateInfoForPack(updateDescriptor);
                Reporter.Output.WriteLine(LocalizableStrings.UpdateApplyStartMessage.Bold().Red());

                if (_updateCoordinator.TryApplyUpdate(updateDescriptor))
                {
                    Reporter.Output.WriteLine(LocalizableStrings.UpdateApplySuccessMessage.Bold().Red());
                    Reporter.Output.WriteLine();
                }
                else
                {
                    anyUpdateFailures = true;
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.TemplateUpdateError, updateDescriptor.InstallUnitDescriptor.Identifier).Bold().Red());
                }
            }

            return !anyUpdateFailures;
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
    }
}
