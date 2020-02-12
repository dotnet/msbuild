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
        public static CreationResultStatus CoordinateHelpAndUsageDisplay(TemplateListResolutionResult templateResolutionResult, IEngineEnvironmentSettings environmentSettings, INewCommandInput commandInput, IHostSpecificDataLoader hostDataLoader, ITelemetryLogger telemetryLogger, TemplateCreator templateCreator, string defaultLanguage)
        {
            ShowUsageHelp(commandInput, telemetryLogger);

            // this is just checking if there is an unambiguous group.
            // the called methods decide whether to get the default language filtered lists, based on what they're doing.
            //
            // The empty TemplateName check is for when only 1 template (or group) is installed.
            // When that occurs, the group is considered partial matches. But the output should be the ambiguous case - list the templates, not help on the singular group.
            if (!string.IsNullOrEmpty(commandInput.TemplateName)
                    && templateResolutionResult.TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousTemplateGroup)
                    && TemplateListResolver.AreAllTemplatesSameLanguage(unambiguousTemplateGroup))
            {
                // This will often show detailed help on the template group, which only makes sense if they're all the same language.
                return DisplayHelpForUnambiguousTemplateGroup(templateResolutionResult, environmentSettings, commandInput, hostDataLoader, templateCreator, defaultLanguage);
            }
            else
            {
                return DisplayHelpForAmbiguousTemplateGroup(templateResolutionResult, environmentSettings, commandInput, hostDataLoader, telemetryLogger, defaultLanguage);
            }
        }

        private static CreationResultStatus DisplayHelpForUnambiguousTemplateGroup(TemplateListResolutionResult templateResolutionResult, IEngineEnvironmentSettings environmentSettings, INewCommandInput commandInput, IHostSpecificDataLoader hostDataLoader, TemplateCreator templateCreator, string defaultLanguage)
        {
            // filter on the default language if needed, the details display should be for a single language group
            if (!templateResolutionResult.TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousTemplateGroupForDetailDisplay))
            {
                // this is really an error
                unambiguousTemplateGroupForDetailDisplay = new List<ITemplateMatchInfo>();
            }

            if (commandInput.IsListFlagSpecified)
            {
                // because the list flag is present, don't display help for the template group, even though an unambiguous group was resolved.
                if (!AreAllParamsValidForAnyTemplateInList(unambiguousTemplateGroupForDetailDisplay)
                    && TemplateListResolver.FindHighestPrecedenceTemplateIfAllSameGroupIdentity(unambiguousTemplateGroupForDetailDisplay) != null)
                {
                    DisplayHelpForAcceptedParameters(commandInput.CommandName);
                    return CreationResultStatus.InvalidParamValues;
                }

                // get the group without filtering on default language
                if (!templateResolutionResult.TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousTemplateGroupForList, true))
                {
                    // this is really an error
                    unambiguousTemplateGroupForList = new List<ITemplateMatchInfo>();
                }

                DisplayTemplateList(unambiguousTemplateGroupForList, environmentSettings, commandInput.Language, defaultLanguage);
                // list flag specified, so no usage examples or detailed help
                return CreationResultStatus.Success;
            }
            else
            {
                // not in list context, but Unambiguous
                // this covers whether or not --help was input, they do the same thing in the unambiguous case
                return TemplateDetailedHelpForSingularTemplateGroup(unambiguousTemplateGroupForDetailDisplay, environmentSettings, commandInput, hostDataLoader, templateCreator);
            }
        }

        private static CreationResultStatus TemplateDetailedHelpForSingularTemplateGroup(IReadOnlyList<ITemplateMatchInfo> unambiguousTemplateGroup, IEngineEnvironmentSettings environmentSettings, INewCommandInput commandInput, IHostSpecificDataLoader hostDataLoader, TemplateCreator templateCreator)
        {
            // (scp 2017-09-06): parse errors probably can't happen in this context.
            foreach (string parseErrorMessage in unambiguousTemplateGroup.Where(x => x.HasParseError()).Select(x => x.GetParseError()).ToList())
            {
                Reporter.Error.WriteLine(parseErrorMessage.Bold().Red());
            }

            GetParametersInvalidForTemplatesInList(unambiguousTemplateGroup, out IReadOnlyList<string> invalidForAllTemplates, out IReadOnlyList<string> invalidForSomeTemplates);

            if (invalidForAllTemplates.Count > 0 || invalidForSomeTemplates.Count > 0)
            {
                DisplayInvalidParameters(invalidForAllTemplates);
                DisplayParametersInvalidForSomeTemplates(invalidForSomeTemplates, LocalizableStrings.SingleTemplateGroupPartialMatchSwitchesNotValidForAllMatches);
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
                && templateResolutionResult.GetBestTemplateMatchList(true).Count > 0
                && templateResolutionResult.GetBestTemplateMatchList(true).All(x => x.MatchDisposition.Any(d => d.Location == MatchLocation.Language && d.Kind == MatchKind.Mismatch)))
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
            IReadOnlyList<ITemplateMatchInfo> templatesForDisplay = templateResolutionResult.GetBestTemplateMatchList(true);
            GetParametersInvalidForTemplatesInList(templatesForDisplay, out IReadOnlyList<string> invalidForAllTemplates, out IReadOnlyList<string> invalidForSomeTemplates);
            if (invalidForAllTemplates.Any() || invalidForSomeTemplates.Any())
            {
                hasInvalidParameters = true;
                DisplayInvalidParameters(invalidForAllTemplates);
                DisplayParametersInvalidForSomeTemplates(invalidForSomeTemplates, LocalizableStrings.PartialTemplateMatchSwitchesNotValidForAllMatches);
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
                return !templateResolutionResult.IsNoTemplatesMatchedState ? CreationResultStatus.Success :  CreationResultStatus.NotFound;
            }
            else
            {
                return CreationResultStatus.OperationNotSpecified;
            }
        }

        // Returns true if any of the input templates has a valid parameter parse result.
        private static bool AreAllParamsValidForAnyTemplateInList(IReadOnlyList<ITemplateMatchInfo> templateList)
        {
            bool anyValidTemplate = false;

            foreach (ITemplateMatchInfo templateInfo in templateList)
            {
                if (templateInfo.GetInvalidParameterNames().Count == 0)
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
        // The columns displayed are as follows:
        // Except where noted, the values are taken from the highest-precedence template in the group. The info could vary among the templates in the group, but shouldn't.
        // (There is no check that the info doesn't vary.)
        // - Template Name
        // - Short Name: displays the first short name from the highest precedence template in the group.
        // - Language: All languages supported by any template in the group are displayed, with the default language in brackets, e.g.: [C#]
        // - Tags
        private static void DisplayTemplateList(IReadOnlyList<ITemplateMatchInfo> templates, IEngineEnvironmentSettings environmentSettings, string language, string defaultLanguage)
        {
            IReadOnlyList<TemplateGroupForListDisplay> groupsForDisplay = GetTemplateGroupsForListDisplay(templates, language, defaultLanguage);

            HelpFormatter<TemplateGroupForListDisplay> formatter =
                HelpFormatter
                    .For(
                        environmentSettings,
                        groupsForDisplay,
                        columnPadding: 6,
                        headerSeparator: '-',
                        blankLineBetweenRows: false)
                    .DefineColumn(t => t.Name, LocalizableStrings.Templates)
                    .DefineColumn(t => t.ShortName, LocalizableStrings.ShortName)
                    .DefineColumn(t => t.Languages, out object languageColumn, LocalizableStrings.Language)
                    .DefineColumn(t => t.Classifications, out object tagsColumn, LocalizableStrings.Tags)
                    .OrderByDescending(languageColumn, new NullOrEmptyIsLastStringComparer())
                    .OrderBy(tagsColumn);
            Reporter.Output.WriteLine(formatter.Layout());
        }

        private class TemplateGroupForListDisplay
        {
            public string Name { get; set; }
            public string ShortName { get; set; }
            public string Languages { get; set; }
            public string Classifications { get; set; }
        }

        private static IReadOnlyList<TemplateGroupForListDisplay> GetTemplateGroupsForListDisplay(IReadOnlyList<ITemplateMatchInfo> templateList, string language, string defaultLanguage)
        {
            IEnumerable<IGrouping<string, ITemplateMatchInfo>> grouped = templateList.GroupBy(x => x.Info.GroupIdentity, x => !string.IsNullOrEmpty(x.Info.GroupIdentity));
            List<TemplateGroupForListDisplay> templateGroupsForDisplay = new List<TemplateGroupForListDisplay>();

            foreach (IGrouping<string, ITemplateMatchInfo> grouping in grouped)
            {
                List<string> languageForDisplay = new List<string>();
                HashSet<string> uniqueLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string defaultLanguageDisplay = string.Empty;

                foreach (ITemplateMatchInfo template in grouping)
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

                ITemplateMatchInfo highestPrecedenceTemplate = grouping.OrderByDescending(x => x.Info.Precedence).First();
                string shortName;
                if (highestPrecedenceTemplate.Info is IShortNameList highestWithShortNameList)
                {
                    shortName = highestWithShortNameList.ShortNameList[0];
                }
                else
                {
                    shortName = highestPrecedenceTemplate.Info.ShortName;
                }

                TemplateGroupForListDisplay groupDisplayInfo = new TemplateGroupForListDisplay()
                {
                    Name = highestPrecedenceTemplate.Info.Name,
                    ShortName = shortName,
                    Languages = string.Join(", ", languageForDisplay),
                    Classifications = highestPrecedenceTemplate.Info.Classifications != null ? string.Join("/", highestPrecedenceTemplate.Info.Classifications) : null
                };
                templateGroupsForDisplay.Add(groupDisplayInfo);
            }

            return templateGroupsForDisplay;
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

        private static void DisplayParametersInvalidForSomeTemplates(IReadOnlyList<string> invalidParams, string messageHeader)
        {
            if (invalidParams.Count > 0)
            {
                Reporter.Error.WriteLine(messageHeader.Bold().Red());
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

            DisplayPartialNameMatchAndContextProblems(templateName, context, templateResolutionResult);
        }

        private static void DisplayPartialNameMatchAndContextProblems(string templateName, string context, TemplateListResolutionResult templateResolutionResult)
        {
            if (templateResolutionResult.IsTemplateAmbiguous)
            {
                // Unable to determine the desired template from the input template name: {0}..
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.AmbiguousInputTemplateName, templateName).Bold().Red());
            }
            else if (templateResolutionResult.IsNoTemplatesMatchedState)
            {
                // No templates matched the input template name: {0}
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.NoTemplatesMatchName, templateName).Bold().Red());
                Reporter.Error.WriteLine();
                return;
            }

            bool anythingReported = false;

            foreach (IReadOnlyList<ITemplateMatchInfo> templateGroup in templateResolutionResult.ContextProblemMatchGroups)
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

            if (templateResolutionResult.RemainingPartialMatchGroups.Count > 0)
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
        public static void GetParametersInvalidForTemplatesInList(IReadOnlyList<ITemplateMatchInfo> templateList, out IReadOnlyList<string> invalidForAllTemplates, out IReadOnlyList<string> invalidForSomeTemplates)
        {
            IDictionary<string, int> invalidCounts = new Dictionary<string, int>();

            foreach (ITemplateMatchInfo template in templateList)
            {
                foreach (string paramName in template.GetInvalidParameterNames())
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

            IEnumerable<IGrouping<string, string>> countGroups = invalidCounts.GroupBy(x => x.Value == templateList.Count ? "all" : "some", x => x.Key);
            invalidForAllTemplates = countGroups.FirstOrDefault(x => string.Equals(x.Key, "all", StringComparison.Ordinal))?.ToList();
            if (invalidForAllTemplates == null)
            {
                invalidForAllTemplates = new List<string>();
            }

            invalidForSomeTemplates = countGroups.FirstOrDefault(x => string.Equals(x.Key, "some", StringComparison.Ordinal))?.ToList();
            if (invalidForSomeTemplates == null)
            {
                invalidForSomeTemplates = new List<string>();
            }
        }

        public static void ShowUsageHelp(INewCommandInput commandInput, ITelemetryLogger telemetryLogger)
        {
            if (commandInput.IsHelpFlagSpecified)
            {
                telemetryLogger.TrackEvent(commandInput.CommandName + "-Help");
            }

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
                // this code path doesn't go through the full help & usage stack, so needs it's own call to ShowUsageHelp().
                ShowUsageHelp(commandInput, telemetryLogger);
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
