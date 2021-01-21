using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateSearch.Common.TemplateUpdate;

namespace Microsoft.TemplateEngine.Cli
{
    internal class TemplateInvocationCoordinator
    {
        private readonly SettingsLoader _settingsLoader;
        private readonly IEngineEnvironmentSettings _environment;
        private readonly INewCommandInput _commandInput;
        private readonly ITelemetryLogger _telemetryLogger;
        private readonly string _commandName;
        private readonly Func<string> _inputGetter;
        private readonly New3Callbacks _callbacks;

        public TemplateInvocationCoordinator(SettingsLoader settingsLoader, INewCommandInput commandInput, ITelemetryLogger telemetryLogger,  string commandName, Func<string> inputGetter, New3Callbacks callbacks)
        {
            _settingsLoader = settingsLoader;
            _environment = _settingsLoader.EnvironmentSettings;
            _commandInput = commandInput;
            _telemetryLogger = telemetryLogger;
            _commandName = commandName;
            _inputGetter = inputGetter;
            _callbacks = callbacks;
        }

        public async Task<CreationResultStatus> CoordinateInvocationOrAcquisitionAsync(ITemplateMatchInfo templateToInvoke)
        {
            // invoke and then check for updates
            CreationResultStatus creationResult = await InvokeTemplateAsync(templateToInvoke).ConfigureAwait(false);
            // check for updates on this template (pack)
            await CheckForTemplateUpdateAsync(templateToInvoke).ConfigureAwait(false);
            return creationResult;
        }

        private Task<CreationResultStatus> InvokeTemplateAsync(ITemplateMatchInfo templateToInvoke)
        {
            TemplateInvoker invoker = new TemplateInvoker(_environment, _commandInput, _telemetryLogger, _commandName, _inputGetter, _callbacks);
            return invoker.InvokeTemplate(templateToInvoke);
        }

        // check for updates for the matched template, based on the Identity
        private async Task CheckForTemplateUpdateAsync(ITemplateMatchInfo templateToInvoke)
        {
            if(!_settingsLoader.InstallUnitDescriptorCache.TryGetDescriptorForTemplate(templateToInvoke.Info, out IInstallUnitDescriptor descriptor))
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.InstallDescriptor_NotFound, templateToInvoke.Info.Identity));
                return;
            }

            List<IInstallUnitDescriptor> descriptorList = new List<IInstallUnitDescriptor>() { descriptor };
            TemplateUpdateChecker updateChecker = new TemplateUpdateChecker(_environment);
            IUpdateCheckResult updateCheckResult = await updateChecker.CheckForUpdatesAsync(descriptorList).ConfigureAwait(false);

            if (updateCheckResult.Updates.Count == 0)
            {
                return;
            }
            else if (updateCheckResult.Updates.Count == 1)
            {
                DisplayUpdateMessage(updateCheckResult.Updates[0]);
            }
            else
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.UpdateCheck_UnknownError, descriptor.Identifier));
            }
        }

        private void DisplayUpdateMessage(IUpdateUnitDescriptor updateDescriptor)
        {
            Reporter.Output.WriteLine();
            Reporter.Output.WriteLine(string.Format(LocalizableStrings.UpdateAvailable, updateDescriptor.UpdateDisplayInfo));
            Reporter.Output.WriteLine(string.Format(LocalizableStrings.UpdateCheck_InstallCommand, _commandName, updateDescriptor.InstallString));
        }
    }
}
