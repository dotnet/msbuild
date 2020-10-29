// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Utils;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using System.Text;

namespace Microsoft.TemplateEngine.Cli.HelpAndUsage
{
    internal static class HelpForTemplateResolution
    {
        public static CreationResultStatus CoordinateHelpAndUsageDisplay(ListOrHelpTemplateListResolutionResult templateResolutionResult, IEngineEnvironmentSettings environmentSettings, INewCommandInput commandInput, IHostSpecificDataLoader hostDataLoader, ITelemetryLogger telemetryLogger, TemplateCreator templateCreator, string defaultLanguage, bool showUsageHelp = true)
        {
            if (showUsageHelp)
            {
                ShowUsageHelp(commandInput, telemetryLogger);
            }

            //in case only --help option is specified we don't need to show templates list
            if (commandInput.IsHelpFlagSpecified && string.IsNullOrEmpty(commandInput.TemplateName))
            {
                return CreationResultStatus.Success; 
            }

            // in case list is specified we always need to list templates 
            if (commandInput.IsListFlagSpecified)
            {
                return DisplayListOrHelpForAmbiguousTemplateGroup(templateResolutionResult, environmentSettings, commandInput, hostDataLoader, telemetryLogger, defaultLanguage);
            }
            else // help flag specified or no flag specified
            {
                if (!string.IsNullOrEmpty(commandInput.TemplateName)
                    && templateResolutionResult.HasUnambiguousTemplateGroup)
                {
                    // This will show detailed help on the template group, which only makes sense if there is a single template group adn all templates are the same language.
                    return DisplayHelpForUnambiguousTemplateGroup(templateResolutionResult, environmentSettings, commandInput, hostDataLoader, templateCreator, telemetryLogger, defaultLanguage);
                }
                else
                {
                    return DisplayListOrHelpForAmbiguousTemplateGroup(templateResolutionResult, environmentSettings, commandInput, hostDataLoader, telemetryLogger, defaultLanguage);
                }

            }
        }

        private static CreationResultStatus DisplayHelpForUnambiguousTemplateGroup(ListOrHelpTemplateListResolutionResult templateResolutionResult, IEngineEnvironmentSettings environmentSettings, INewCommandInput commandInput, IHostSpecificDataLoader hostDataLoader, TemplateCreator templateCreator, ITelemetryLogger telemetryLogger, string defaultLanguage)
        {
            // sanity check: should never happen; as condition for unambiguous template group is checked above
            if (!templateResolutionResult.UnambiguousTemplateGroup.Any())
            {
                return DisplayListOrHelpForAmbiguousTemplateGroup(templateResolutionResult, environmentSettings, commandInput, hostDataLoader, telemetryLogger, defaultLanguage);
            }

            //if language is specified and all templates in unambigiuos group match the language show the help for that template
            if (templateResolutionResult.AllTemplatesInUnambiguousTemplateGroupAreSameLanguage)
            {
                IReadOnlyCollection<ITemplateMatchInfo> unambiguousTemplateGroupForDetailDisplay = templateResolutionResult.UnambiguousTemplateGroup;
                return TemplateDetailedHelpForSingularTemplateGroup(unambiguousTemplateGroupForDetailDisplay, environmentSettings, commandInput, hostDataLoader, templateCreator);
            }
            //if language is not specified and group has template that matches the language show the help for that the template that matches the language
            if (string.IsNullOrEmpty(commandInput.Language) && !string.IsNullOrEmpty(defaultLanguage) && templateResolutionResult.HasUnambiguousTemplateGroupForDefaultLanguage)
            {
                IReadOnlyCollection<ITemplateMatchInfo> unambiguousTemplateGroupForDetailDisplay = templateResolutionResult.UnambiguousTemplatesForDefaultLanguage;
                return TemplateDetailedHelpForSingularTemplateGroup(unambiguousTemplateGroupForDetailDisplay, environmentSettings, commandInput, hostDataLoader, templateCreator);
            }
            else
            {
                return DisplayListOrHelpForAmbiguousTemplateGroup(templateResolutionResult, environmentSettings, commandInput, hostDataLoader, telemetryLogger, defaultLanguage);
            }
        }
       

        private static CreationResultStatus TemplateDetailedHelpForSingularTemplateGroup(IReadOnlyCollection<ITemplateMatchInfo> unambiguousTemplateGroup, IEngineEnvironmentSettings environmentSettings, INewCommandInput commandInput, IHostSpecificDataLoader hostDataLoader, TemplateCreator templateCreator)
        {
            // sanity check: should never happen; as condition for unambiguous template group is checked above
            if (!unambiguousTemplateGroup.Any())
            {
                return CreationResultStatus.NotFound;
            }
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

            if (invalidForAllTemplates.Count == 0)
            {
                bool showImplicitlyHiddenParams = unambiguousTemplateGroup.Count > 1;
                TemplateDetailsDisplay.ShowTemplateGroupHelp(unambiguousTemplateGroup, environmentSettings, commandInput, hostDataLoader, templateCreator, showImplicitlyHiddenParams);
            }
            else
            {
                Reporter.Error.WriteLine(
                    string.Format(LocalizableStrings.InvalidParameterTemplateHint,  GetTemplateHelpCommand(commandInput.CommandName, unambiguousTemplateGroup.First().Info)).Bold().Red());
            }

            return invalidForAllTemplates.Count > 0 || invalidForSomeTemplates.Count > 0
                ? CreationResultStatus.InvalidParamValues
                : CreationResultStatus.Success;
        }

        private static CreationResultStatus DisplayListOrHelpForAmbiguousTemplateGroup(ListOrHelpTemplateListResolutionResult templateResolutionResult, IEngineEnvironmentSettings environmentSettings, INewCommandInput commandInput, IHostSpecificDataLoader hostDataLoader, ITelemetryLogger telemetryLogger, string defaultLanguage)
        {
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
            IReadOnlyCollection<ITemplateMatchInfo> templatesForDisplay = templateResolutionResult.ExactMatchedTemplates;
            GetParametersInvalidForTemplatesInList(templatesForDisplay, out IReadOnlyList<string> invalidForAllTemplates, out IReadOnlyList<string> invalidForSomeTemplates);
            if (invalidForAllTemplates.Any() || invalidForSomeTemplates.Any())
            {
                hasInvalidParameters = true;
                DisplayInvalidParameters(invalidForAllTemplates);
                DisplayParametersInvalidForSomeTemplates(invalidForSomeTemplates, LocalizableStrings.PartialTemplateMatchSwitchesNotValidForAllMatches);
            }


            if (templateResolutionResult.HasExactMatches)
            {
                IReadOnlyCollection<IGrouping<string, ITemplateMatchInfo>> groupedTemplatesForDisplay = templateResolutionResult.ExactMatchedTemplatesGrouped;
                ShowTemplatesFoundMessage(commandInput.TemplateName, commandInput.Language, commandInput.TypeFilter, commandInput.BaselineName);
                DisplayTemplateList(groupedTemplatesForDisplay, environmentSettings, commandInput, defaultLanguage);
            }
            else
            {
                ShowContextAndTemplateNameMismatchHelp(templateResolutionResult, commandInput.TemplateName, commandInput.Language, commandInput.TypeFilter, commandInput.BaselineName);
            }

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
                return templateResolutionResult.HasExactMatches ? CreationResultStatus.Success :  CreationResultStatus.NotFound;
            }
            else
            {
                return CreationResultStatus.OperationNotSpecified;
            }
        }

        // Returns true if any of the input templates has a valid parameter parse result.
        private static bool AreAllParamsValidForAnyTemplateInList(IReadOnlyCollection<ITemplateMatchInfo> templateList)
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
        private static void DisplayTemplateList(IReadOnlyCollection<IGrouping<string, ITemplateMatchInfo>> templates, IEngineEnvironmentSettings environmentSettings, INewCommandInput commandInput, string defaultLanguage)
        {
            IReadOnlyCollection<TemplateGroupForListDisplay> groupsForDisplay = GetTemplateGroupsForListDisplay(templates, commandInput.Language, defaultLanguage);

            HelpFormatter<TemplateGroupForListDisplay> formatter =
                HelpFormatter
                    .For(
                        environmentSettings,
                        commandInput,
                        groupsForDisplay,
                        columnPadding: 2,
                        headerSeparator: '-',
                        blankLineBetweenRows: false)
                    .DefineColumn(t => t.Name, LocalizableStrings.Templates, shrinkIfNeeded: true, minWidth:25, showAlways: true)
                    .DefineColumn(t => t.ShortName, LocalizableStrings.ShortName, showAlways: true)
                    .DefineColumn(t => t.Languages, out object languageColumn,  LocalizableStrings.Language, NewCommandInputCli.LanguageColumnFilter, defaultColumn: true)
                    .DefineColumn(t => t.Type, LocalizableStrings.ColumnNameType, NewCommandInputCli.TypeColumnFilter, defaultColumn: false)
                    .DefineColumn(t => t.Author,  LocalizableStrings.ColumnNameAuthor, NewCommandInputCli.AuthorColumnFilter, defaultColumn: false)
                    .DefineColumn(t => t.Classifications, out object tagsColumn, LocalizableStrings.Tags, NewCommandInputCli.TagsColumnFilter, defaultColumn: true)

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
            public string Author { get; set; }
            public string Type { get; set; }
        }

        private static IReadOnlyList<TemplateGroupForListDisplay> GetTemplateGroupsForListDisplay(IReadOnlyCollection<IGrouping<string, ITemplateMatchInfo>> groupedTemplateList, string language, string defaultLanguage)
        {
            List<TemplateGroupForListDisplay> templateGroupsForDisplay = new List<TemplateGroupForListDisplay>();

            foreach (IGrouping<string, ITemplateMatchInfo> grouping in groupedTemplateList)
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

                string templateType = string.Empty;
                if (highestPrecedenceTemplate.Info.Tags != null && highestPrecedenceTemplate.Info.Tags.TryGetValue("type", out ICacheTag typeTag))
                {
                    templateType = typeTag.DefaultValue;
                }

                TemplateGroupForListDisplay groupDisplayInfo = new TemplateGroupForListDisplay()
                {
                    Name = highestPrecedenceTemplate.Info.Name,
                    ShortName = shortName,
                    Languages = string.Join(",", languageForDisplay),
                    Classifications = highestPrecedenceTemplate.Info.Classifications != null ? string.Join("/", highestPrecedenceTemplate.Info.Classifications) : null,
                    Author = highestPrecedenceTemplate.Info.Author,
                    Type = templateType
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

        private static void ShowContextAndTemplateNameMismatchHelp(ListOrHelpTemplateListResolutionResult templateResolutionResult, string templateName, string templateLanguage, string context, string baselineName)
        {
            if (string.IsNullOrEmpty(templateName) && string.IsNullOrEmpty(templateLanguage) && string.IsNullOrEmpty(context) && string.IsNullOrEmpty(baselineName))
            {
                return;
            }
            DisplayPartialNameMatchLanguageAndContextProblems(templateName, templateLanguage, context, templateResolutionResult, baselineName);
        }

        private static void DisplayPartialNameMatchLanguageAndContextProblems(string templateName, string templateLanguage, string context, ListOrHelpTemplateListResolutionResult templateResolutionResult, string baselineName)
        {
            bool anythingReported = false;
            if (templateResolutionResult.HasExactMatches)
            {
                return;
            }
            else
            {
                //
                ShowNoTemplatesFoundMessage(templateName, templateLanguage, context, baselineName);
                anythingReported = true;
            }

            if (templateResolutionResult.HasPartialMatches)
            {
                // {0} template(s) partially matched, but failed on {1}.
                Reporter.Error.WriteLine(
                    string.Format(
                        LocalizableStrings.TemplatesNotValidGivenTheSpecifiedFilter,
                        templateResolutionResult.PartiallyMatchedTemplatesGrouped.Count,
                        GetPartialMatchReason(templateResolutionResult, templateLanguage, context, baselineName))
                    .Bold().Red());

                anythingReported = true;
            }

            if (anythingReported)
            {
                Reporter.Error.WriteLine();
            }
        }

        // Returns a list of the parameter names that are invalid for every template in the input group.
        public static void GetParametersInvalidForTemplatesInList(IReadOnlyCollection<ITemplateMatchInfo> templateList, out IReadOnlyList<string> invalidForAllTemplates, out IReadOnlyList<string> invalidForSomeTemplates)
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
            else if (invalidParams.Count > 0)
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.RunHelpForInformationAboutAcceptedParameters, commandInput.CommandName).Bold().Red());
            }
            if (commandInput.HasColumnsParseError)
            {
                Reporter.Error.WriteLine(commandInput.ColumnsParseError.Bold().Red());
            }

            return CreationResultStatus.InvalidParamValues;
        }

        internal static string GetTemplateHelpCommand(string commandName, ITemplateInfo template)
        {
            return $"dotnet {commandName} {template.ShortName} --help";
        }

        private static string GetInputParametersString(string templateName, string templateLanguage, string context, string baselineName)
        {
            StringBuilder inputParametersString = new StringBuilder();
            string separator = ", ";

            if (!string.IsNullOrEmpty(templateName))
            {
                inputParametersString.AppendFormat($"'{templateName}'");
            }
            if (!string.IsNullOrEmpty(templateLanguage))
            {
                inputParametersString.Append(separator).AppendFormat($"language='{templateLanguage}'");
            }
            if (!string.IsNullOrEmpty(context))
            {
                inputParametersString.Append(separator).AppendFormat($"type='{context}'");
            }
            if (!string.IsNullOrEmpty(baselineName))
            {
                inputParametersString.Append(separator).AppendFormat($"baseline='{baselineName}'");
            }
            return string.IsNullOrEmpty(templateName)
                ? inputParametersString.ToString(separator.Length, inputParametersString.Length - separator.Length)
                : inputParametersString.ToString();
        }

        private static void ShowNoTemplatesFoundMessage(string templateName, string templateLanguage, string context, string baselineName)
        {
            // No templates found matching the following input parameter(s): {0}.
            // To list installed templates: dotnet new --list.
            Reporter.Error.WriteLine(string.Format(LocalizableStrings.NoTemplatesMatchingInputParameters, GetInputParametersString(templateName, templateLanguage, context, baselineName)).Bold().Red());
            Reporter.Error.WriteLine(LocalizableStrings.ListTemplatesCommand.Bold().Red());
        }

        private static void ShowTemplatesFoundMessage(string templateName, string templateLanguage, string context, string baselineName)
        {
            if (!string.IsNullOrEmpty(templateName) || !string.IsNullOrEmpty(templateLanguage) || !string.IsNullOrEmpty(context) || !string.IsNullOrEmpty(baselineName))
            {
                // Templates found matching the following input parameter(s): {0}
                Reporter.Output.WriteLine(string.Format(LocalizableStrings.TemplatesFoundMatchingInputParameters, GetInputParametersString(templateName, templateLanguage, context, baselineName)));
                Reporter.Output.WriteLine();
            }
        }

        private static string GetPartialMatchReason(ListOrHelpTemplateListResolutionResult templateResolutionResult, string templateLanguage, string context, string baselineName)
        {
            StringBuilder reason = new StringBuilder();
            string separator = ", ";

            if (templateResolutionResult.HasLanguageMismatch)
            {
                reason.Append(separator).AppendFormat($"language='{templateLanguage}'");
            }
            if (templateResolutionResult.HasContextMismatch)
            {
                reason.Append(separator).AppendFormat($"type='{context}'");
            }
            if (templateResolutionResult.HasBaselineMismatch)
            {
                reason.Append(separator).AppendFormat($"baseline='{baselineName}'");
            }

            return reason.Length != 0
                ? reason.ToString(separator.Length, reason.Length - separator.Length)
                : string.Empty;
        }
    }
}
