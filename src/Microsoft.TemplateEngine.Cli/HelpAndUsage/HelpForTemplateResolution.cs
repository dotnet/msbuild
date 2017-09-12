// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.HelpAndUsage
{
    internal static class HelpForTemplateResolution
    {
        public static CreationResultStatus CoordinateHelpAndUsageDisplay(TemplateListResolutionResult templateResolutionResult, IEngineEnvironmentSettings environmentSettings, INewCommandInput commandInput, IHostSpecificDataLoader hostDataLoader, ITelemetryLogger telemeteryLogger, TemplateCreator templateCreator, string defaultLanguage)
        {
            if (templateResolutionResult.TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<IFilteredTemplateInfo> unambiguousTemplateGroup))
            {
                return DisplayHelpForUnambiguousTemplateGroup(templateResolutionResult, unambiguousTemplateGroup, environmentSettings, commandInput, hostDataLoader, templateCreator, defaultLanguage);
            }
            else
            {
                return DisplayHelpForAmbiguousTemplateGroup(templateResolutionResult, environmentSettings, commandInput, hostDataLoader, telemeteryLogger, defaultLanguage);
            }
        }

        private static CreationResultStatus DisplayHelpForUnambiguousTemplateGroup(TemplateListResolutionResult templateResolutionResult, IReadOnlyList<IFilteredTemplateInfo> unambiguousTemplateGroup, IEngineEnvironmentSettings environmentSettings, INewCommandInput commandInput, IHostSpecificDataLoader hostDataLoader, TemplateCreator templateCreator, string defaultLanguage)
        {
            if (commandInput.IsListFlagSpecified)
            {
                // because the list flag is present, don't display help for the template group, even though an unambiguous group was resolved.
                if (!AreAllParamsValidForAnyTemplateInList(unambiguousTemplateGroup)
                    && TemplateListResolver.FindHighestPrecedenceTemplateIfAllSameGroupIdentity(unambiguousTemplateGroup) != null)
                {
                    DisplayHelpForAcceptedParameters(commandInput.CommandName);
                    return CreationResultStatus.InvalidParamValues;
                }

                DisplayTemplateList(unambiguousTemplateGroup, environmentSettings, commandInput.Language, defaultLanguage);
                // list flag specified, so no usage examples or detailed help
                return CreationResultStatus.Success;
            }
            else
            {
                // not in list context, but Unambiguous
                // this covers whether or not --help was input, they do the same thing in the unambiguous case
                return TemplateDetailedHelpForSingularTemplateGroup(unambiguousTemplateGroup, environmentSettings, commandInput, hostDataLoader, templateCreator);
            }
        }

        private static CreationResultStatus TemplateDetailedHelpForSingularTemplateGroup(IReadOnlyList<IFilteredTemplateInfo> unambiguousTemplateGroup, IEngineEnvironmentSettings environmentSettings, INewCommandInput commandInput, IHostSpecificDataLoader hostDataLoader, TemplateCreator templateCreator)
        {
            ShowUsageHelp(commandInput);

            // (scp 2017-09-06): parse errors probably can't happen in this context.
            foreach (string parseErrorMessage in unambiguousTemplateGroup.Where(x => x.HasParseError).Select(x => x.ParseError).ToList())
            {
                Reporter.Error.WriteLine(parseErrorMessage.Bold().Red());
            }

            GetParametersInvalidForTemplatesInList(unambiguousTemplateGroup, out IReadOnlyList<string> invalidForAllTemplates, out IReadOnlyList<string> invalidForSomeTemplates);

            if (invalidForAllTemplates.Count > 0)
            {
                DisplayInvalidParameters(invalidForAllTemplates);
            }

            bool showImplicitlyHiddenParams = unambiguousTemplateGroup.Count > 1;
            TemplateDetailsDisplay.ShowTemplateGroupHelp(unambiguousTemplateGroup, environmentSettings, commandInput, hostDataLoader, templateCreator, showImplicitlyHiddenParams);

            return invalidForAllTemplates.Count > 0 || invalidForSomeTemplates.Count > 0
                ? CreationResultStatus.InvalidParamValues
                : CreationResultStatus.Success;
        }

        private static CreationResultStatus DisplayHelpForAmbiguousTemplateGroup(TemplateListResolutionResult templateResolutionResult, IEngineEnvironmentSettings environmentSettings, INewCommandInput commandInput, IHostSpecificDataLoader hostDataLoader, ITelemetryLogger telemetryLogger, string defaultLanguage)
        {
            if (!string.IsNullOrEmpty(commandInput.TemplateName)
                && templateResolutionResult.BestTemplateMatchList.Count > 0
                && templateResolutionResult.BestTemplateMatchList.All(x => x.MatchDisposition.Any(d => d.Location == MatchLocation.Language && d.Kind == MatchKind.Mismatch)))
            {
                string errorMessage = GetLanguageMismatchErrorMessage(commandInput);
                Reporter.Error.WriteLine(errorMessage.Bold().Red());
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.RunHelpForInformationAboutAcceptedParameters, $"{commandInput.CommandName} {commandInput.TemplateName}").Bold().Red());
                return CreationResultStatus.NotFound;
            }

            // The following occurs when:
            //      --alias <value> is specifed
            //      --help is specified
            //      template (group) can't be resolved
            if (!string.IsNullOrWhiteSpace(commandInput.Alias))
            {
                Reporter.Error.WriteLine(LocalizableStrings.InvalidInputSwitch.Bold().Red());
                Reporter.Error.WriteLine("  " + commandInput.TemplateParamInputFormat("--alias").Bold().Red());
                return CreationResultStatus.NotFound;
            }

            bool hasInvalidParameters = false;
            IReadOnlyList<IFilteredTemplateInfo> templatesForDisplay = templateResolutionResult.BestTemplateMatchList;
            GetParametersInvalidForTemplatesInList(templatesForDisplay, out IReadOnlyList<string> invalidForAllTemplates, out IReadOnlyList<string> invalidForSomeTemplates);
            if (invalidForAllTemplates.Any() || invalidForSomeTemplates.Any())
            {
                hasInvalidParameters = true;
                DisplayInvalidParameters(invalidForAllTemplates);
                DisplayParametersInvalidForSomeTemplates(invalidForSomeTemplates);
            }

            if (commandInput.IsHelpFlagSpecified)
            {
                // usage help should show first (if it's being shown at all).
                telemetryLogger.TrackEvent(commandInput.CommandName + "-Help");
                ShowUsageHelp(commandInput);
            }

            ShowContextAndTemplateNameMismatchHelp(templateResolutionResult, commandInput.TemplateName, commandInput.TypeFilter);
            DisplayTemplateList(templatesForDisplay, environmentSettings, commandInput.Language, defaultLanguage);

            if (!commandInput.IsListFlagSpecified)
            {
                TemplateUsageHelp.ShowInvocationExamples(templateResolutionResult, hostDataLoader, commandInput.CommandName);
            }

            if (hasInvalidParameters)
            {
                return CreationResultStatus.NotFound;
            }
            else if (commandInput.IsListFlagSpecified || commandInput.IsHelpFlagSpecified)
            {
                return CreationResultStatus.Success;
            }
            else
            {
                return CreationResultStatus.OperationNotSpecified;
            }
        }

        // Returns true if any of the input templates has a valid parameter parse result.
        private static bool AreAllParamsValidForAnyTemplateInList(IReadOnlyList<IFilteredTemplateInfo> templateList)
        {
            bool anyValidTemplate = false;

            foreach (IFilteredTemplateInfo templateInfo in templateList)
            {
                if (templateInfo.InvalidParameterNames.Count == 0)
                {
                    anyValidTemplate = true;
                    break;
                }
            }

            return anyValidTemplate;
        }

        private static void DisplayHelpForAcceptedParameters(string commandName)
        {
            Reporter.Error.WriteLine(string.Format(LocalizableStrings.RunHelpForInformationAboutAcceptedParameters, commandName).Bold().Red());
        }

        // Displays the list of templates in a table, one row per template group.
        // 
        // The columns displayed are as follows
        // Note: Except language, the values are taken from one template in the group. The info could vary among the templates in the group, but shouldn't.
        // There is no check that the info doesn't vary.
        //  - Templates
        //  - Short Name
        //  - Language: All languages supported by the group are displayed, with the default language in brackets, e.g.: [C#]
        //  - Tags
        private static void DisplayTemplateList(IReadOnlyList<IFilteredTemplateInfo> templates, IEngineEnvironmentSettings environmentSettings, string language, string defaultLanguage)
        {
            IReadOnlyDictionary<ITemplateInfo, string> templateGroupsLanguages = GetLanguagesForTemplateGroups(templates, language, defaultLanguage);

            HelpFormatter<KeyValuePair<ITemplateInfo, string>> formatter = HelpFormatter.For(environmentSettings, templateGroupsLanguages, 6, '-', false)
                .DefineColumn(t => t.Key.Name, LocalizableStrings.Templates)
                .DefineColumn(t => t.Key.ShortName, LocalizableStrings.ShortName)
                .DefineColumn(t => t.Value, out object languageColumn, LocalizableStrings.Language)
                .DefineColumn(t => t.Key.Classifications != null ? string.Join("/", t.Key.Classifications) : null, out object tagsColumn, LocalizableStrings.Tags)
                .OrderByDescending(languageColumn, new NullOrEmptyIsLastStringComparer())
                .OrderBy(tagsColumn);
            Reporter.Output.WriteLine(formatter.Layout());
        }

        private static IReadOnlyDictionary<ITemplateInfo, string> GetLanguagesForTemplateGroups(IReadOnlyList<IFilteredTemplateInfo> templateList, string language, string defaultLanguage)
        {
            IEnumerable<IGrouping<string, IFilteredTemplateInfo>> grouped = templateList.GroupBy(x => x.Info.GroupIdentity, x => !string.IsNullOrEmpty(x.Info.GroupIdentity));
            Dictionary<ITemplateInfo, string> templateGroupsLanguages = new Dictionary<ITemplateInfo, string>();

            foreach (IGrouping<string, IFilteredTemplateInfo> grouping in grouped)
            {
                List<string> languageForDisplay = new List<string>();
                HashSet<string> uniqueLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string defaultLanguageDisplay = string.Empty;

                foreach (IFilteredTemplateInfo template in grouping)
                {
                    if (template.Info.Tags != null && template.Info.Tags.TryGetValue("language", out ICacheTag languageTag))
                    {
                        foreach (string lang in languageTag.ChoicesAndDescriptions.Keys)
                        {
                            if (uniqueLanguages.Add(lang))
                            {
                                if (string.IsNullOrEmpty(language) && string.Equals(defaultLanguage, lang, StringComparison.OrdinalIgnoreCase))
                                {
                                    defaultLanguageDisplay = $"[{lang}]";
                                }
                                else
                                {
                                    languageForDisplay.Add(lang);
                                }
                            }
                        }
                    }
                }

                languageForDisplay.Sort(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrEmpty(defaultLanguageDisplay))
                {
                    languageForDisplay.Insert(0, defaultLanguageDisplay);
                }

                templateGroupsLanguages[grouping.First().Info] = string.Join(", ", languageForDisplay);
            }

            return templateGroupsLanguages;
        }

        public static void DisplayInvalidParameters(IReadOnlyList<string> invalidParams)
        {
            if (invalidParams.Count > 0)
            {
                Reporter.Error.WriteLine(LocalizableStrings.InvalidInputSwitch.Bold().Red());
                foreach (string flag in invalidParams)
                {
                    Reporter.Error.WriteLine($"  {flag}".Bold().Red());
                }
            }
        }

        private static void DisplayParametersInvalidForSomeTemplates(IReadOnlyList<string> invalidParams)
        {
            if (invalidParams.Count > 0)
            {
                Reporter.Error.WriteLine(LocalizableStrings.PartialTemplateMatchSwitchesNotValidForAllMatches.Bold().Red());
                foreach (string flag in invalidParams)
                {
                    Reporter.Error.WriteLine($"  {flag}".Bold().Red());
                }
            }
        }

        private static void ShowContextAndTemplateNameMismatchHelp(TemplateListResolutionResult templateResolutionResult, string templateName, string context)
        {
            if (string.IsNullOrEmpty(templateName))
            {
                return;
            }

            GetContextBasedAndOtherPartialMatches(templateResolutionResult, out IReadOnlyList<IReadOnlyList<IFilteredTemplateInfo>> contextProblemMatchGroups, out IReadOnlyList<IReadOnlyList<IFilteredTemplateInfo>> remainingPartialMatchGroups);
            DisplayPartialNameMatchAndContextProblems(templateName, context, contextProblemMatchGroups, remainingPartialMatchGroups);
        }

        private static void GetContextBasedAndOtherPartialMatches(TemplateListResolutionResult templateResolutionResult, out IReadOnlyList<IReadOnlyList<IFilteredTemplateInfo>> contextProblemMatchGroups, out IReadOnlyList<IReadOnlyList<IFilteredTemplateInfo>> remainingPartialMatchGroups)
        {
            Dictionary<string, List<IFilteredTemplateInfo>> contextProblemMatches = new Dictionary<string, List<IFilteredTemplateInfo>>();
            Dictionary<string, List<IFilteredTemplateInfo>> remainingPartialMatches = new Dictionary<string, List<IFilteredTemplateInfo>>();

            // this filtering / grouping ignores language differences.
            foreach (IFilteredTemplateInfo template in templateResolutionResult.BestTemplateMatchList)
            {
                if (template.MatchDisposition.Any(x => x.Location == MatchLocation.Context && x.Kind != MatchKind.Exact))
                {
                    if (!contextProblemMatches.TryGetValue(template.Info.GroupIdentity, out List<IFilteredTemplateInfo> templateGroup))
                    {
                        templateGroup = new List<IFilteredTemplateInfo>();
                        contextProblemMatches.Add(template.Info.GroupIdentity, templateGroup);
                    }

                    templateGroup.Add(template);
                }
                else if (!templateResolutionResult.UsingContextMatches
                    && template.MatchDisposition.Any(t => t.Location != MatchLocation.Context && t.Kind != MatchKind.Mismatch && t.Kind != MatchKind.Unspecified))
                {
                    if (!remainingPartialMatches.TryGetValue(template.Info.GroupIdentity, out List<IFilteredTemplateInfo> templateGroup))
                    {
                        templateGroup = new List<IFilteredTemplateInfo>();
                        remainingPartialMatches.Add(template.Info.GroupIdentity, templateGroup);
                    }

                    templateGroup.Add(template);
                }
            }

            // context mismatches from the "matched" templates
            contextProblemMatchGroups = contextProblemMatches.Values.ToList();

            // other templates with anything matching
            remainingPartialMatchGroups = remainingPartialMatches.Values.ToList();
        }

        private static void DisplayPartialNameMatchAndContextProblems(string templateName, string context, IReadOnlyList<IReadOnlyList<IFilteredTemplateInfo>> contextProblemMatchGroups, IReadOnlyList<IReadOnlyList<IFilteredTemplateInfo>> remainingPartialMatchGroups)
        {
            if (contextProblemMatchGroups.Count + remainingPartialMatchGroups.Count > 1)
            {
                // Unable to determine the desired template from the input template name: {0}..
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.AmbiguousInputTemplateName, templateName));
            }
            else if (contextProblemMatchGroups.Count + remainingPartialMatchGroups.Count == 0)
            {
                // No templates matched the input template name: {0}
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.NoTemplatesMatchName, templateName));
                Reporter.Error.WriteLine();
                return;
            }

            bool anythingReported = false;

            foreach (IReadOnlyList<IFilteredTemplateInfo> templateGroup in contextProblemMatchGroups)
            {
                // all templates in a group should have the same context & name
                if (templateGroup[0].Info.Tags != null && templateGroup[0].Info.Tags.TryGetValue("type", out ICacheTag typeTag))
                {
                    MatchInfo? matchInfo = WellKnownSearchFilters.ContextFilter(context)(templateGroup[0].Info);
                    if ((matchInfo?.Kind ?? MatchKind.Mismatch) == MatchKind.Mismatch)
                    {
                        // {0} matches the specified name, but has been excluded by the --type parameter. Remove or change the --type parameter to use that template
                        Reporter.Error.WriteLine(string.Format(LocalizableStrings.TemplateNotValidGivenTheSpecifiedFilter, templateGroup[0].Info.Name).Bold().Red());
                        anythingReported = true;
                    }
                }
                else
                {
                    // this really shouldn't ever happen. But better to have a generic error than quietly ignore the partial match.
                    //
                    //{0} cannot be created in the target location
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.GenericPlaceholderTemplateContextError, templateGroup[0].Info.Name).Bold().Red());
                    anythingReported = true;
                }
            }

            if (remainingPartialMatchGroups.Count > 0)
            {
                // The following templates partially match the input. Be more specific with the template name and/or language
                Reporter.Error.WriteLine(LocalizableStrings.TemplateMultiplePartialNameMatches.Bold().Red());
                anythingReported = true;
            }

            if (anythingReported)
            {
                Reporter.Error.WriteLine();
            }
        }

        // Returns a list of the parameter names that are invalid for every template in the input group.
        private static void GetParametersInvalidForTemplatesInList(IReadOnlyList<IFilteredTemplateInfo> templateList, out IReadOnlyList<string> invalidForAllTemplates, out IReadOnlyList<string> invalidForSomeTemplates)
        {
            IDictionary<string, int> invalidCounts = new Dictionary<string, int>();

            foreach (IFilteredTemplateInfo template in templateList)
            {
                foreach (string paramName in template.InvalidParameterNames)
                {
                    if (!invalidCounts.ContainsKey(paramName))
                    {
                        invalidCounts[paramName] = 1;
                    }
                    else
                    {
                        invalidCounts[paramName]++;
                    }
                }
            }

            invalidForAllTemplates = invalidCounts.Where(x => x.Value == templateList.Count).Select(x => x.Key).ToList();
            invalidForSomeTemplates = invalidCounts.Where(x => x.Value != templateList.Count).Select(x => x.Key).ToList();
        }

        public static void ShowUsageHelp(INewCommandInput commandInput)
        {
            Reporter.Output.WriteLine(commandInput.HelpText);
            Reporter.Output.WriteLine();
        }

        public static CreationResultStatus HandleParseError(INewCommandInput commandInput, ITelemetryLogger telemetryLogger)
        {
            TemplateListResolver.ValidateRemainingParameters(commandInput, out IReadOnlyList<string> invalidParams);
            DisplayInvalidParameters(invalidParams);

            // TODO: get a meaningful error message from the parser
            if (commandInput.IsHelpFlagSpecified)
            {
                telemetryLogger.TrackEvent(commandInput.CommandName + "-Help");
                ShowUsageHelp(commandInput);
            }
            else
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.RunHelpForInformationAboutAcceptedParameters, commandInput.CommandName).Bold().Red());
            }

            return CreationResultStatus.InvalidParamValues;
        }

        private static string GetLanguageMismatchErrorMessage(INewCommandInput commandInput)
        {
            string inputFlagForm;
            if (commandInput.Tokens.Contains("-lang"))
            {
                inputFlagForm = "-lang";
            }
            else
            {
                inputFlagForm = "--language";
            }

            string invalidLanguageErrorText = LocalizableStrings.InvalidTemplateParameterValues;
            invalidLanguageErrorText += Environment.NewLine + string.Format(LocalizableStrings.InvalidParameterDetail, inputFlagForm, commandInput.Language, "language");
            return invalidLanguageErrorText;
        }
    }
}
