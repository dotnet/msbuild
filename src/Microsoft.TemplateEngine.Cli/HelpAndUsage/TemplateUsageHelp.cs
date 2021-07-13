// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;
using TemplateCreator = Microsoft.TemplateEngine.Edge.Template.TemplateCreator;

namespace Microsoft.TemplateEngine.Cli.HelpAndUsage
{
    internal static class TemplateUsageHelp
    {
        // TODO: rework this method... it's a bit of a god-method, for very specific purposes.
        // Number of times I've deferred on reworking this method: 4
        internal static async Task<TemplateUsageInformation?> GetTemplateUsageInformationAsync(
            ITemplateInfo templateInfo,
            IEngineEnvironmentSettings environmentSettings,
            INewCommandInput commandInput,
            IHostSpecificDataLoader hostDataLoader,
            TemplateCreator templateCreator,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IParameterSet allParams;
            IReadOnlyList<string> userParamsWithInvalidValues;
            HashSet<string> userParamsWithDefaultValues;
            bool hasPostActionScriptRunner;

            ITemplate? template = templateCreator.LoadTemplate(templateInfo, commandInput.BaselineName);

            if (template == null)
            {
                return null;
            }

            HostSpecificTemplateData hostData = hostDataLoader.ReadHostSpecificTemplateData(templateInfo);
            TemplateCommandInput reparsedCommand = TemplateCommandInput.ParseForTemplate(templateInfo, commandInput, hostData);

#pragma warning disable CS0618 // Type or member is obsolete
            allParams = templateCreator.SetupDefaultParamValuesFromTemplateAndHost(template, template.DefaultName ?? "testName", out IReadOnlyList<string> defaultParamsWithInvalidValues);
            templateCreator.ResolveUserParameters(template, allParams, reparsedCommand.InputTemplateParams, out userParamsWithInvalidValues);
            hasPostActionScriptRunner = await CheckIfTemplateHasScriptRunningPostActionsAsync(template, environmentSettings, reparsedCommand, templateCreator, cancellationToken).ConfigureAwait(false);
            templateCreator.ReleaseMountPoints(template);
#pragma warning restore CS0618 // Type or member is obsolete

            List<InvalidParameterInfo> invalidParameters = new List<InvalidParameterInfo>();

            if (userParamsWithInvalidValues.Any())
            {
                // Lookup the input param formats - userParamsWithInvalidValues has canonical.
                foreach (string canonical in userParamsWithInvalidValues)
                {
                    reparsedCommand.InputTemplateParams.TryGetValue(canonical, out string? specifiedValue);
                    string inputFormat = reparsedCommand.TemplateParamInputFormat(canonical);
                    InvalidParameterInfo invalidParam = new InvalidParameterInfo(InvalidParameterInfo.Kind.InvalidParameterValue, inputFormat, specifiedValue, canonical);
                    invalidParameters.Add(invalidParam);
                }
            }

#pragma warning disable CS0618 // Type or member is obsolete
            if (templateCreator.AnyParametersWithInvalidDefaultsUnresolved(defaultParamsWithInvalidValues, userParamsWithInvalidValues, reparsedCommand.InputTemplateParams, out IReadOnlyList<string> defaultsWithUnresolvedInvalidValues))
#pragma warning restore CS0618 // Type or member is obsolete
            {
                IParameterSet templateParams = template.Generator.GetParametersForTemplate(environmentSettings, template);

                foreach (string defaultParamName in defaultsWithUnresolvedInvalidValues)
                {
                    ITemplateParameter? param = templateParams.ParameterDefinitions.FirstOrDefault(x => string.Equals(x.Name, defaultParamName, StringComparison.Ordinal));

                    if (param != null)
                    {
                        // Get the best input format available.
                        IReadOnlyList<string> inputVariants = reparsedCommand.VariantsForCanonical(param.Name);
                        string displayName = inputVariants.FirstOrDefault(x => x.Contains(param.Name))
                            ?? inputVariants.Aggregate("", (max, cur) => max.Length > cur.Length ? max : cur)
                            ?? param.Name;

                        InvalidParameterInfo invalidParam = new InvalidParameterInfo(InvalidParameterInfo.Kind.InvalidDefaultValue, displayName, param.DefaultValue, displayName);
                        invalidParameters.Add(invalidParam);
                    }
                }
            }

            // get all the flags
            // get all the user input params that have the default value
            Dictionary<string, IReadOnlyList<string>> inputFlagVariants = new Dictionary<string, IReadOnlyList<string>>();
            userParamsWithDefaultValues = new HashSet<string>();
            foreach (string paramName in allParams.ParameterDefinitions.Select(x => x.Name))
            {
                inputFlagVariants[paramName] = reparsedCommand.VariantsForCanonical(paramName);

                if (reparsedCommand.InputTemplateParams.ContainsKey(paramName) && string.IsNullOrEmpty(reparsedCommand.InputTemplateParams[paramName]))
                {
                    userParamsWithDefaultValues.Add(paramName);
                }
            }
            IReadOnlyDictionary<string, IReadOnlyList<string>> variantsForCanonicals = inputFlagVariants;

            return new TemplateUsageInformation
            {
                InvalidParameters = invalidParameters,
                AllParameters = allParams,
                UserParametersWithInvalidValues = userParamsWithInvalidValues,
                UserParametersWithDefaultValues = userParamsWithDefaultValues,
                VariantsForCanonicals = variantsForCanonicals,
                HasPostActionScriptRunner = hasPostActionScriptRunner
            };
        }

        private static async Task<bool> CheckIfTemplateHasScriptRunningPostActionsAsync(
            ITemplate template,
            IEngineEnvironmentSettings environmentSettings,
            TemplateCommandInput commandInput,
            TemplateCreator templateCreator,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // use a throwaway set of params for getting the creation effects - it makes changes to them.
            string targetDir = commandInput.OutputPath ?? environmentSettings.Host.FileSystem.GetCurrentDirectory();
#pragma warning disable CS0618 // Type or member is obsolete
            IParameterSet paramsForCreationEffects = templateCreator.SetupDefaultParamValuesFromTemplateAndHost(template, template.DefaultName ?? "testName", out IReadOnlyList<string> throwaway);
            templateCreator.ResolveUserParameters(template, paramsForCreationEffects, commandInput.InputTemplateParams, out IReadOnlyList<string> userParamsWithInvalidValues);
#pragma warning restore CS0618 // Type or member is obsolete
            ICreationEffects creationEffects = await template.Generator.GetCreationEffectsAsync(
                environmentSettings,
                template,
                paramsForCreationEffects,
                targetDir,
                default).ConfigureAwait(false);
            return creationEffects.CreationResult.PostActions.Any(x => x.ActionId == ProcessStartPostActionProcessor.ActionProcessorId);
        }
    }
}
