// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Cli.HelpAndUsage;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli
{
    internal class TemplateInvocationCoordinator
    {
        private readonly IEngineEnvironmentSettings _environment;
        private readonly ITelemetryLogger _telemetryLogger;
        private readonly string? _defaultLanguage;
        private readonly Func<string> _inputGetter;
        private readonly New3Callbacks _callbacks;
        private readonly TemplatePackageManager _templatePackageManager;
        private readonly TemplateInformationCoordinator _templateInformationCoordinator;
        private readonly IHostSpecificDataLoader _hostSpecificDataLoader;
        private readonly TemplateInvoker _invoker;

        internal TemplateInvocationCoordinator(
            IEngineEnvironmentSettings environment,
            TemplatePackageManager templatePackageManager,
            TemplateInformationCoordinator templateInformationCoordinator,
            IHostSpecificDataLoader hostSpecificDataLoader,
            ITelemetryLogger telemetryLogger,
            string? defaultLanguage,
            Func<string> inputGetter,
            New3Callbacks callbacks)
        {
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _telemetryLogger = telemetryLogger ?? throw new ArgumentNullException(nameof(telemetryLogger));
            _defaultLanguage = defaultLanguage;
            _inputGetter = inputGetter ?? throw new ArgumentNullException(nameof(inputGetter));
            _callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
            _templatePackageManager = templatePackageManager ?? throw new ArgumentNullException(nameof(templatePackageManager));
            _templateInformationCoordinator = templateInformationCoordinator ?? throw new ArgumentNullException(nameof(templateInformationCoordinator));
            _hostSpecificDataLoader = hostSpecificDataLoader ?? throw new ArgumentNullException(nameof(hostSpecificDataLoader));
            _invoker = new TemplateInvoker(_environment, _telemetryLogger, _inputGetter, _callbacks, _hostSpecificDataLoader);
        }

        internal async Task<New3CommandStatus> CoordinateInvocationAsync(INewCommandInput commandInput, CancellationToken cancellationToken)
        {
            InstantiateTemplateResolver resolver = new InstantiateTemplateResolver(_templatePackageManager, _hostSpecificDataLoader);
            TemplateResolutionResult templateResolutionResult = await resolver.ResolveTemplatesAsync(commandInput, _defaultLanguage, default).ConfigureAwait(false);

            if (templateResolutionResult.ResolutionStatus == TemplateResolutionResult.Status.SingleMatch && templateResolutionResult.TemplateToInvoke != null)
            {
                TemplatePackageCoordinator packageCoordinator = new TemplatePackageCoordinator(_telemetryLogger, _environment, _templatePackageManager, _templateInformationCoordinator);

                // start checking for updates
                var checkForUpdateTask = packageCoordinator.CheckUpdateForTemplate(templateResolutionResult.TemplateToInvoke.Value.Template, commandInput, cancellationToken);
                // start creation of template
                var templateCreationTask = _invoker.InvokeTemplate(
                    templateResolutionResult.TemplateToInvoke.Value.Template,
                    templateResolutionResult.TemplateToInvoke.Value.Parameters,
                    commandInput);

                // await for both tasks to finish
                await Task.WhenAll(checkForUpdateTask, templateCreationTask).ConfigureAwait(false);

                if (checkForUpdateTask.Result != null)
                {
                    // print if there is update for this template
                    packageCoordinator.DisplayUpdateCheckResult(checkForUpdateTask.Result, commandInput);
                }

                // return creation result
                return templateCreationTask.Result;
            }
            else
            {
                return await _templateInformationCoordinator.CoordinateAmbiguousTemplateResolutionDisplayAsync(templateResolutionResult, commandInput, default).ConfigureAwait(false);
            }
        }
    }
}
