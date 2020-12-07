// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateEngine.Cli.HelpAndUsage
{
    public static class TemplateUsageHelp
    {
        public static void ShowInvocationExamples(TemplateListResolutionResult templateResolutionResult, IHostSpecificDataLoader hostDataLoader, string commandName)
        {
            const int ExamplesToShow = 2;
            IReadOnlyList<string> preferredNameList = new List<string>() { "mvc" };
            int numShown = 0;

            IReadOnlyCollection<ITemplateMatchInfo> bestMatchedTemplates = templateResolutionResult.ExactMatchedTemplates;

            if (bestMatchedTemplates.Count == 0)
            {
                return;
            }

            IList<ITemplateInfo> templateList = bestMatchedTemplates.Select(x => x.Info).ToList();
            Reporter.Output.WriteLine("Examples:");
            HashSet<string> usedGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string preferredName in preferredNameList)
            {
                ITemplateInfo template = templateList.FirstOrDefault(x => string.Equals(x.ShortName, preferredName, StringComparison.OrdinalIgnoreCase));

                if (template != null)
                {
                    string identity = string.IsNullOrWhiteSpace(template.GroupIdentity) ? string.IsNullOrWhiteSpace(template.Identity) ? string.Empty : template.Identity : template.GroupIdentity;
                    if (usedGroupIds.Add(identity))
                    {
                        GenerateUsageForTemplate(template, hostDataLoader, commandName);
                        numShown++;
                    }
                }

                templateList.Remove(template);  // remove it so it won't get chosen again
            }

            // show up to 2 examples (total, including the above)
            Random rnd = new Random();
            for (int i = numShown; i < ExamplesToShow && templateList.Count > 0; i++)
            {
                int index = rnd.Next(0, templateList.Count - 1);
                ITemplateInfo template = templateList[index];
                string identity = string.IsNullOrWhiteSpace(template.GroupIdentity) ? string.IsNullOrWhiteSpace(template.Identity) ? string.Empty : template.Identity : template.GroupIdentity;
                if (usedGroupIds.Add(identity) && !GenerateUsageForTemplate(template, hostDataLoader, commandName))
                {
                    --i;
                }

                templateList.Remove(template);  // remove it so it won't get chosen again
            }

            // show a help example
            Reporter.Output.WriteLine($"    dotnet {commandName} --help");

            // show a help example for template
            Reporter.Output.WriteLine($"    {HelpForTemplateResolution.GetTemplateHelpCommand(commandName, bestMatchedTemplates.First().Info)}");
            
        }

        private static bool GenerateUsageForTemplate(ITemplateInfo templateInfo, IHostSpecificDataLoader hostDataLoader, string commandName)
        {
            HostSpecificTemplateData hostTemplateData = hostDataLoader.ReadHostSpecificTemplateData(templateInfo);

            if (hostTemplateData.UsageExamples != null)
            {
                if (hostTemplateData.UsageExamples.Count == 0)
                {
                    return false;
                }

                Reporter.Output.WriteLine($"    dotnet {commandName} {templateInfo.ShortName} {hostTemplateData.UsageExamples[0]}");
                return true;
            }

            Reporter.Output.Write($"    dotnet {commandName} {templateInfo.ShortName}");
            IReadOnlyList<ITemplateParameter> allParameterDefinitions = templateInfo.Parameters;
            IEnumerable<ITemplateParameter> filteredParams = TemplateParameterHelpBase.FilterParamsForHelp(allParameterDefinitions, hostTemplateData.HiddenParameterNames, parametersToAlwaysShow: hostTemplateData.ParametersToAlwaysShow);

            foreach (ITemplateParameter parameter in filteredParams)
            {
                if (string.Equals(parameter.DataType, "bool", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(parameter.DefaultValue, "false", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                else if (string.Equals(parameter.DataType, "string", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                else if (string.Equals(parameter.DataType, "choice", StringComparison.OrdinalIgnoreCase) && parameter.Choices.Count == 1)
                {
                    continue;
                }

                string displayParameter = hostTemplateData.DisplayNameForParameter(parameter.Name);

                Reporter.Output.Write($" --{displayParameter}");

                if (!string.IsNullOrEmpty(parameter.DefaultValue) && !string.Equals(parameter.DataType, "bool", StringComparison.OrdinalIgnoreCase))
                {
                    Reporter.Output.Write($" {parameter.DefaultValue}");
                }
            }

            Reporter.Output.WriteLine();
            return true;
        }

        // TODO: rework this method... it's a bit of a god-method, for very specific purposes.
        // Number of times I've deferred on reworking this method: 4
        internal static TemplateUsageInformation? GetTemplateUsageInformation(ITemplateInfo templateInfo, IEngineEnvironmentSettings environmentSettings, INewCommandInput commandInput, IHostSpecificDataLoader hostDataLoader, TemplateCreator templateCreator)
        {
            IParameterSet allParams;
            IReadOnlyList<string> userParamsWithInvalidValues;
            HashSet<string> userParamsWithDefaultValues;
            bool hasPostActionScriptRunner;

            ITemplate template = environmentSettings.SettingsLoader.LoadTemplate(templateInfo, commandInput.BaselineName);

            if (template == null)
            {
                return null;
            }

            TemplateResolver.ParseTemplateArgs(templateInfo, hostDataLoader, commandInput);
            allParams = templateCreator.SetupDefaultParamValuesFromTemplateAndHost(template, template.DefaultName ?? "testName", out IReadOnlyList<string> defaultParamsWithInvalidValues);
            templateCreator.ResolveUserParameters(template, allParams, commandInput.InputTemplateParams, out userParamsWithInvalidValues);
            hasPostActionScriptRunner = CheckIfTemplateHasScriptRunningPostActions(template, environmentSettings, commandInput, templateCreator);
            templateCreator.ReleaseMountPoints(template);

            List<InvalidParameterInfo> invalidParameters = new List<InvalidParameterInfo>();

            if (userParamsWithInvalidValues.Any())
            {
                // Lookup the input param formats - userParamsWithInvalidValues has canonical.
                foreach (string canonical in userParamsWithInvalidValues)
                {
                    commandInput.InputTemplateParams.TryGetValue(canonical, out string specifiedValue);
                    string inputFormat = commandInput.TemplateParamInputFormat(canonical);
                    InvalidParameterInfo invalidParam = new InvalidParameterInfo(inputFormat, specifiedValue, canonical);
                    invalidParameters.Add(invalidParam);
                }
            }

            if (templateCreator.AnyParametersWithInvalidDefaultsUnresolved(defaultParamsWithInvalidValues, userParamsWithInvalidValues, commandInput.InputTemplateParams, out IReadOnlyList<string> defaultsWithUnresolvedInvalidValues))
            {
                IParameterSet templateParams = template.Generator.GetParametersForTemplate(environmentSettings, template);

                foreach (string defaultParamName in defaultsWithUnresolvedInvalidValues)
                {
                    ITemplateParameter param = templateParams.ParameterDefinitions.FirstOrDefault(x => string.Equals(x.Name, defaultParamName, StringComparison.Ordinal));

                    if (param != null)
                    {
                        // Get the best input format available.
                        IReadOnlyList<string> inputVariants = commandInput.VariantsForCanonical(param.Name);
                        string displayName = inputVariants.FirstOrDefault(x => x.Contains(param.Name))
                            ?? inputVariants.Aggregate("", (max, cur) => max.Length > cur.Length ? max : cur)
                            ?? param.Name;

                        InvalidParameterInfo invalidParam = new InvalidParameterInfo(displayName, param.DefaultValue, displayName, true);
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
                inputFlagVariants[paramName] = commandInput.VariantsForCanonical(paramName);

                if (commandInput.TemplateParamHasValue(paramName) && string.IsNullOrEmpty(commandInput.TemplateParamValue(paramName)))
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

        private static bool CheckIfTemplateHasScriptRunningPostActions(ITemplate template, IEngineEnvironmentSettings environmentSettings, INewCommandInput commandInput, TemplateCreator templateCreator)
        {
            // use a throwaway set of params for getting the creation effects - it makes changes to them.
            string targetDir = commandInput.OutputPath ?? environmentSettings.Host.FileSystem.GetCurrentDirectory();
            IParameterSet paramsForCreationEffects = templateCreator.SetupDefaultParamValuesFromTemplateAndHost(template, template.DefaultName ?? "testName", out IReadOnlyList<string> throwaway);
            templateCreator.ResolveUserParameters(template, paramsForCreationEffects, commandInput.InputTemplateParams, out IReadOnlyList<string> userParamsWithInvalidValues);
            ICreationEffects creationEffects = template.Generator.GetCreationEffects(environmentSettings, template, paramsForCreationEffects, environmentSettings.SettingsLoader.Components, targetDir);
            return creationEffects.CreationResult.PostActions.Any(x => x.ActionId == ProcessStartPostActionProcessor.ActionProcessorId);
        }
    }
}
