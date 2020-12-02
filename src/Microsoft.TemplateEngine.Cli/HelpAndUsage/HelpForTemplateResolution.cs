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
using Microsoft.TemplateEngine.Cli.TableOutput;

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
                    string.Format(LocalizableStrings.InvalidParameterTemplateHint, GetTemplateHelpCommand(commandInput.CommandName, unambiguousTemplateGroup.First().Info)).Bold().Red());
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
                ShowTemplatesFoundMessage(commandInput);
                DisplayTemplateList(templateResolutionResult.ExactMatchedTemplates, environmentSettings, commandInput, defaultLanguage);
            }
            else
            {
                ShowContextAndTemplateNameMismatchHelp(templateResolutionResult, commandInput);
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
                return templateResolutionResult.HasExactMatches ? CreationResultStatus.Success : CreationResultStatus.NotFound;
            }
            else
            {
                return CreationResultStatus.OperationNotSpecified;
            }
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
        private static void DisplayTemplateList(IReadOnlyCollection<ITemplateMatchInfo> templates, IEngineEnvironmentSettings environmentSettings, INewCommandInput commandInput, string defaultLanguage)
        {
            IReadOnlyCollection<TemplateGroupTableRow> groupsForDisplay = TemplateGroupDisplay.GetTemplateGroupsForListDisplay(templates, commandInput.Language, defaultLanguage);

            HelpFormatter<TemplateGroupTableRow> formatter =
                HelpFormatter
                    .For(
                        environmentSettings,
                        commandInput,
                        groupsForDisplay,
                        columnPadding: 2,
                        headerSeparator: '-',
                        blankLineBetweenRows: false)
                    .DefineColumn(t => t.Name, LocalizableStrings.ColumnNameTemplateName, shrinkIfNeeded: true, minWidth: 15, showAlways: true)
                    .DefineColumn(t => t.ShortName, LocalizableStrings.ColumnNameShortName, showAlways: true)
                    .DefineColumn(t => t.Languages, out object languageColumn,  LocalizableStrings.ColumnNameLanguage, NewCommandInputCli.LanguageColumnFilter, defaultColumn: true)
                    .DefineColumn(t => t.Type, LocalizableStrings.ColumnNameType, NewCommandInputCli.TypeColumnFilter, defaultColumn: false)
                    .DefineColumn(t => t.Author,  LocalizableStrings.ColumnNameAuthor, NewCommandInputCli.AuthorColumnFilter, defaultColumn: false, shrinkIfNeeded: true, minWidth: 10)
                    .DefineColumn(t => t.Classifications, out object tagsColumn, LocalizableStrings.ColumnNameTags, NewCommandInputCli.TagsColumnFilter, defaultColumn: true)

                    .OrderByDescending(languageColumn, new NullOrEmptyIsLastStringComparer())
                    .OrderBy(tagsColumn);
            Reporter.Output.WriteLine(formatter.Layout());
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

        private static void ShowContextAndTemplateNameMismatchHelp(ListOrHelpTemplateListResolutionResult templateResolutionResult, INewCommandInput commandInput)
        {
            if (string.IsNullOrEmpty(commandInput.TemplateName) && SupportedFilterOptions.SupportedListFilters.All(filter => !filter.IsFilterSet(commandInput)))
            {
                return;
            }
            if (templateResolutionResult.HasExactMatches)
            {
                return;
            }

            // No templates found matching the following input parameter(s): {0}.
            Reporter.Error.WriteLine(string.Format(LocalizableStrings.NoTemplatesMatchingInputParameters, GetInputParametersString(commandInput)).Bold().Red());

            if (templateResolutionResult.HasPartialMatches)
            {
                // {0} template(s) partially matched, but failed on {1}.
                Reporter.Error.WriteLine(
                    string.Format(
                        LocalizableStrings.TemplatesNotValidGivenTheSpecifiedFilter,
                        templateResolutionResult.PartiallyMatchedTemplatesGrouped.Count,
                        GetPartialMatchReason(templateResolutionResult, commandInput))
                    .Bold().Red());
            }

            if (!commandInput.IsListFlagSpecified)
            {
                // To list installed templates, run 'dotnet {0} --list'.
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.ListTemplatesCommand, commandInput.CommandName).Bold().Red());
            }

            // To search for the templates on NuGet.org, run 'dotnet {0} <template name> --search'.
            Reporter.Error.WriteLine(string.Format(LocalizableStrings.SearchTemplatesCommand, commandInput.CommandName, commandInput.TemplateName).Bold().Red());
            Reporter.Error.WriteLine();
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

        private static string GetInputParametersString(INewCommandInput commandInput)
        {
            string separator = ", ";
            string filters = string.Join(
                separator,
                SupportedFilterOptions.SupportedListFilters
                    .Where(filter => filter.IsFilterSet(commandInput))
                    .Select(filter => $"{filter.Name}='{filter.FilterValue(commandInput)}'"));
            return string.IsNullOrEmpty(commandInput.TemplateName)
                ? filters
                : string.IsNullOrEmpty(filters)
                    ? $"'{commandInput.TemplateName}'"
                    : $"'{commandInput.TemplateName}'" + separator + filters;
        }

        private static void ShowTemplatesFoundMessage(INewCommandInput commandInput)
        {
            if (!string.IsNullOrWhiteSpace(commandInput.TemplateName) || SupportedFilterOptions.SupportedListFilters.Any(filter => filter.IsFilterSet(commandInput)))
            {
                // Templates found matching the following input parameter(s): {0}
                Reporter.Output.WriteLine(string.Format(LocalizableStrings.TemplatesFoundMatchingInputParameters, GetInputParametersString(commandInput)));
                Reporter.Output.WriteLine();
            }
        }

        private static string GetPartialMatchReason(ListOrHelpTemplateListResolutionResult templateResolutionResult, INewCommandInput commandInput)
        {
            string separator = ", ";
            return string.Join(separator,
                SupportedFilterOptions.SupportedListFilters
                .OfType<TemplateFilterOption>()
                .Where(filter => filter.IsFilterSet(commandInput) && filter.MismatchCriteria(templateResolutionResult))
                .Select(filter => $"{filter.Name}='{filter.FilterValue(commandInput)}'"));
        }
    }
}
