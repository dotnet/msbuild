// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Cli.CommandParsing;

namespace Microsoft.TemplateEngine.Cli
{
    internal class TemplateInvocationCoordinator
    {
        private readonly ISettingsLoader _settingsLoader;
        private readonly IEngineEnvironmentSettings _environment;
        private readonly INewCommandInput _commandInput;
        private readonly ITelemetryLogger _telemetryLogger;
        private readonly string _commandName;
        private readonly Func<string> _inputGetter;
        private readonly New3Callbacks _callbacks;

        internal TemplateInvocationCoordinator(ISettingsLoader settingsLoader, INewCommandInput commandInput, ITelemetryLogger telemetryLogger, string commandName, Func<string> inputGetter, New3Callbacks callbacks)
        {
            _settingsLoader = settingsLoader;
            _environment = _settingsLoader.EnvironmentSettings;
            _commandInput = commandInput;
            _telemetryLogger = telemetryLogger;
            _commandName = commandName;
            _inputGetter = inputGetter;
            _callbacks = callbacks;
        }

        internal async Task<New3CommandStatus> CoordinateInvocationOrAcquisitionAsync(ITemplateMatchInfo templateToInvoke, CancellationToken cancellationToken)
        {
            TemplatePackageCoordinator packageCoordinator = new TemplatePackageCoordinator(_telemetryLogger, _environment);

            // start checking for updates
            var checkForUpdateTask = packageCoordinator.CheckUpdateForTemplate(templateToInvoke.Info, _commandInput, cancellationToken);

            // start creation of template
            var templateCreationTask = InvokeTemplateAsync(templateToInvoke);

            // await for both tasks to finish
            await Task.WhenAll(checkForUpdateTask, templateCreationTask).ConfigureAwait(false);

            if (checkForUpdateTask.Result != null)
            {
                // print if there is update for this template
                packageCoordinator.DisplayUpdateCheckResult(checkForUpdateTask.Result, _commandInput);
            }

            // return creation result
            return templateCreationTask.Result;
        }

        private Task<New3CommandStatus> InvokeTemplateAsync(ITemplateMatchInfo templateToInvoke)
        {
            TemplateInvoker invoker = new TemplateInvoker(_environment, _commandInput, _telemetryLogger, _commandName, _inputGetter, _callbacks);
            return invoker.InvokeTemplate(templateToInvoke);
        }
    }
}
