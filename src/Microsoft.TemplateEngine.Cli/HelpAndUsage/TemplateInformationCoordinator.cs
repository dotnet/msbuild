// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Cli.TableOutput;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;
using TemplateCreator = Microsoft.TemplateEngine.Edge.Template.TemplateCreator;

namespace Microsoft.TemplateEngine.Cli.HelpAndUsage
{
    internal class TemplateInformationCoordinator
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;
        private readonly TemplatePackageManager _templatePackageManager;
        private readonly TemplateCreator _templateCreator;
        private readonly IHostSpecificDataLoader _hostSpecificDataLoader;
        private readonly ITelemetryLogger _telemetryLogger;
        private readonly string? _defaultLanguage;

        internal TemplateInformationCoordinator(
            IEngineEnvironmentSettings engineEnvironmentSettings,
            TemplatePackageManager templatePackageManager,
            TemplateCreator templateCreator,
            IHostSpecificDataLoader hostSpecificDataLoader,
            ITelemetryLogger telemetryLogger,
            string? defaultLanguage)

        {
            _engineEnvironmentSettings = engineEnvironmentSettings ?? throw new ArgumentNullException(nameof(engineEnvironmentSettings));
            _templatePackageManager = templatePackageManager ?? throw new ArgumentNullException(nameof(templatePackageManager));
            _templateCreator = templateCreator ?? throw new ArgumentNullException(nameof(templateCreator));
            _hostSpecificDataLoader = hostSpecificDataLoader ?? throw new ArgumentNullException(nameof(hostSpecificDataLoader));
            _telemetryLogger = telemetryLogger ?? throw new ArgumentNullException(nameof(telemetryLogger));
            _defaultLanguage = defaultLanguage;
        }

        /// <summary>
        /// Displays the help in case it is not possible to resolve template to use based on user input.
        /// Used for template instantiation and template help.
        /// </summary>
        /// <param name="resolutionResult">template resolution result.</param>
        /// <param name="commandInput">command input used in CLI.</param>
        /// <param name="cancellationToken">cancellation token.</param>
        /// <returns></returns>
        internal Task<New3CommandStatus> CoordinateAmbiguousTemplateResolutionDisplayAsync(
            TemplateResolutionResult resolutionResult,
            INewCommandInput commandInput,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (resolutionResult.ResolutionStatus)
            {
                case TemplateResolutionResult.Status.NoMatch:
                    Reporter.Error.WriteLine(
                        string.Format(LocalizableStrings.NoTemplatesMatchingInputParameters, GetInputParametersString(commandInput)).Bold().Red());
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.ListTemplatesCommand, commandInput.ListCommandExample()).Bold().Red());
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.SearchTemplatesCommand, commandInput.SearchCommandExample(commandInput.TemplateName)).Bold().Red());
                    return Task.FromResult(New3CommandStatus.NotFound);
                case TemplateResolutionResult.Status.AmbiguousLanguageChoice:
                    Reporter.Error.WriteLine(LocalizableStrings.AmbiguousTemplateGroupListHeader.Bold().Red());
                    DisplayTemplateList(resolutionResult.TemplateGroups, commandInput, useErrorOutput: true);
                    Reporter.Error.WriteLine(LocalizableStrings.AmbiguousLanguageHint.Bold().Red());
                    return Task.FromResult(New3CommandStatus.NotFound);
                case TemplateResolutionResult.Status.AmbiguousTemplateGroupChoice:
                    Reporter.Error.WriteLine(LocalizableStrings.AmbiguousTemplateGroupListHeader.Bold().Red());
                    DisplayTemplateList(resolutionResult.TemplateGroups, commandInput, useErrorOutput: true);
                    //TODO: https://github.com/dotnet/templating/issues/3275
                    //revise error handling: this message is not the best CTA
                    //Reporter.Error.WriteLine(LocalizableStrings.AmbiguousTemplateGroupListHint.Bold().Red());
                    return Task.FromResult(New3CommandStatus.NotFound);
                case TemplateResolutionResult.Status.AmbiguousParameterValueChoice:
                    if (resolutionResult.UnambiguousTemplateGroup == null)
                    {
                        throw new ArgumentException($"{nameof(resolutionResult.UnambiguousTemplateGroup)} should not be null if {nameof(resolutionResult.ResolutionStatus)} is {nameof(TemplateResolutionResult.Status.AmbiguousParameterValueChoice)}");
                    }
                    Reporter.Verbose.WriteLine(LocalizableStrings.Authoring_AmbiguousChoiceParameterValue);
                    return Task.FromResult(DisplayInvalidParameterError(resolutionResult.UnambiguousTemplateGroup, commandInput));
                case TemplateResolutionResult.Status.AmbiguousTemplateChoice:
                    if (resolutionResult.UnambiguousTemplateGroup == null)
                    {
                        throw new ArgumentException($"{nameof(resolutionResult.UnambiguousTemplateGroup)} should not be null if {nameof(resolutionResult.ResolutionStatus)} is {nameof(TemplateResolutionResult.Status.AmbiguousParameterValueChoice)}");
                    }
                    Reporter.Verbose.WriteLine(LocalizableStrings.Authoring_AmbiguousBestPrecedence);
                    return DisplayAmbiguousPrecedenceErrorAsync(resolutionResult.UnambiguousTemplateGroup, commandInput, cancellationToken);
                case TemplateResolutionResult.Status.InvalidParameter:
                    if (resolutionResult.UnambiguousTemplateGroup == null)
                    {
                        throw new ArgumentException($"{nameof(resolutionResult.UnambiguousTemplateGroup)} should not be null if {nameof(resolutionResult.ResolutionStatus)} is {nameof(TemplateResolutionResult.Status.AmbiguousParameterValueChoice)}");
                    }
                    return Task.FromResult(DisplayInvalidParameterError(resolutionResult.UnambiguousTemplateGroup, commandInput));
            }
            return Task.FromResult(New3CommandStatus.CreateFailed);
        }

        /// <summary>
        /// Displays the list of templates in a table, one row per template group.
        ///
        /// The columns displayed are as follows:
        /// Except where noted, the values are taken from the highest-precedence template in the group. The info could vary among the templates in the group, but shouldn't.
        /// (There is no check that the info doesn't vary.)
        /// - Template Name
        /// - Short Name: displays the all available short names for the group.
        /// - Language: All languages supported by any template in the group are displayed, with the default language in brackets, e.g.: [C#]
        /// - Tags
        /// The columns can be configured in <see cref="INewCommandInput.Columns"/> and <see cref="INewCommandInput.ShowAllColumns"/>.
        /// </summary>
        internal void DisplayTemplateList(
            IReadOnlyCollection<TemplateGroup> templateGroups,
            INewCommandInput commandInput,
            bool useErrorOutput = false)
        {
            IReadOnlyCollection<TemplateGroupTableRow> groupsForDisplay = TemplateGroupDisplay.GetTemplateGroupsForListDisplay(templateGroups, commandInput.Language, _defaultLanguage);
            DisplayTemplateList(groupsForDisplay, commandInput, useErrorOutput);
        }

        /// <summary>
        /// Displays the list of templates in a table, one row per template group.
        ///
        /// The columns displayed are as follows:
        /// Except where noted, the values are taken from the highest-precedence template in the group. The info could vary among the templates in the group, but shouldn't.
        /// (There is no check that the info doesn't vary.)
        /// - Template Name
        /// - Short Name: displays the all available short names for the group.
        /// - Language: All languages supported by any template in the group are displayed, with the default language in brackets, e.g.: [C#]
        /// - Tags
        /// The columns can be configured in <see cref="INewCommandInput.Columns"/> and <see cref="INewCommandInput.ShowAllColumns"/>.
        /// </summary>
        internal void DisplayTemplateList(
            IEnumerable<ITemplateInfo> templates,
            INewCommandInput commandInput,
            bool useErrorOutput = false)
        {
            IReadOnlyCollection<TemplateGroupTableRow> groupsForDisplay = TemplateGroupDisplay.GetTemplateGroupsForListDisplay(templates, commandInput.Language, _defaultLanguage);
            DisplayTemplateList(groupsForDisplay, commandInput, useErrorOutput);
        }

#pragma warning disable SA1204 // Static elements should appear before instance elements
        internal static void DisplayInvalidParameters(IEnumerable<string> invalidParams)
#pragma warning restore SA1204 // Static elements should appear before instance elements
        {
            _ = invalidParams ?? throw new ArgumentNullException(nameof(invalidParams));

            if (invalidParams.Any())
            {
                Reporter.Error.WriteLine(LocalizableStrings.InvalidInputSwitch.Bold().Red());
                foreach (string flag in invalidParams)
                {
                    Reporter.Error.WriteLine($"  {flag}".Bold().Red());
                }
            }
        }

        /// <summary>
        /// Returns a list of the parameter names that are invalid for every template in the input group.
        /// </summary>
        internal static void GetParametersInvalidForTemplatesInList(IReadOnlyCollection<ITemplateMatchInfo> templateList, out IReadOnlyList<string> invalidForAllTemplates, out IReadOnlyList<string> invalidForSomeTemplates)
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
            invalidForAllTemplates =
                countGroups.FirstOrDefault(x => string.Equals(x.Key, "all", StringComparison.Ordinal))?.ToList()
                ?? new List<string>();

            invalidForSomeTemplates =
                countGroups.FirstOrDefault(x => string.Equals(x.Key, "some", StringComparison.Ordinal))?.ToList()
                ?? new List<string>();
        }

        internal void ShowUsageHelp(INewCommandInput commandInput)
        {
            if (commandInput.IsHelpFlagSpecified)
            {
                _telemetryLogger.TrackEvent(commandInput.CommandName + "-Help");
            }

            Reporter.Output.WriteLine(commandInput.HelpText);
            Reporter.Output.WriteLine();
        }

        internal New3CommandStatus HandleParseError(INewCommandInput commandInput)
        {
            TemplateResolver.ValidateRemainingParameters(commandInput, out IReadOnlyList<string> invalidParams);
            DisplayInvalidParameters(invalidParams);

            // TODO: get a meaningful error message from the parser
            if (commandInput.IsHelpFlagSpecified)
            {
                // this code path doesn't go through the full help & usage stack, so needs it's own call to ShowUsageHelp().
                ShowUsageHelp(commandInput);
            }
            else if (invalidParams.Count > 0)
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.RunHelpForInformationAboutAcceptedParameters, commandInput.CommandName).Bold().Red());
            }
            if (commandInput.HasColumnsParseError)
            {
                Reporter.Error.WriteLine(commandInput.ColumnsParseError.Bold().Red());
            }

            return New3CommandStatus.InvalidParamValues;
        }

        /// <summary>
        /// Returns the help command for given template; or null in case template does not have short name defined.
        /// </summary>
        /// <param name="commandName"></param>
        /// <param name="template"></param>
        /// <returns>the help command or null in case template does not have short name defined.</returns>
#pragma warning disable SA1204 // Static elements should appear before instance elements
        internal static string? GetTemplateHelpCommand(string commandName, ITemplateInfo template)
#pragma warning restore SA1204 // Static elements should appear before instance elements
        {
            if (template.ShortNameList.Any())
            {
                return GetTemplateHelpCommand(commandName, template.ShortNameList[0]);
            }
            return null;
        }

        /// <summary>
        /// Handles help display for the template (dotnet new3 template-name --help).
        /// </summary>
        /// <param name="commandInput">user command input.</param>
        /// <param name="cancellationToken">cancellation token.</param>
        /// <returns><see cref="New3CommandStatus"/> for operation.</returns>
        internal async Task<New3CommandStatus> DisplayDetailedHelpAsync(
            INewCommandInput commandInput,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TemplateResolutionResult helpResolutionResult = TemplateResolver.GetTemplateResolutionResultForHelp(await _templatePackageManager.GetTemplatesAsync(default).ConfigureAwait(false), _hostSpecificDataLoader, commandInput, _defaultLanguage);
            if (helpResolutionResult.GroupResolutionStatus == TemplateResolutionResult.UnambiguousTemplateGroupStatus.SingleMatch && helpResolutionResult.TemplatesForDetailedHelp.Any())
            {
                IReadOnlyCollection<ITemplateMatchInfo> unambiguousTemplateGroup = helpResolutionResult.TemplatesForDetailedHelp.ToList();
                bool showImplicitlyHiddenParams = unambiguousTemplateGroup.Count > 1;
                return await TemplateDetailsDisplay.ShowTemplateGroupHelpAsync(unambiguousTemplateGroup, _engineEnvironmentSettings, commandInput, _hostSpecificDataLoader, _templateCreator, showImplicitlyHiddenParams, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                return await CoordinateAmbiguousTemplateResolutionDisplayAsync(helpResolutionResult, commandInput, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Handles template list display (dotnet new3 --list).
        /// </summary>
        /// <param name="commandInput">user command input.</param>
        /// <param name="cancellationToken">cancellation token.</param>
        /// <returns></returns>
        internal async Task<New3CommandStatus> DisplayTemplateGroupListAsync(
            INewCommandInput commandInput,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TemplateListResolutionResult listingTemplateResolutionResult = TemplateResolver.GetTemplateResolutionResultForList(
               await _templatePackageManager.GetTemplatesAsync(default).ConfigureAwait(false),
               _hostSpecificDataLoader,
               commandInput,
               _defaultLanguage);

            // The following occurs when:
            //      --alias <value> is specified
            //      --help is specified
            //      template (group) can't be resolved
            if (!string.IsNullOrWhiteSpace(commandInput.Alias))
            {
                Reporter.Error.WriteLine(LocalizableStrings.InvalidInputSwitch.Bold().Red());
                Reporter.Error.WriteLine("  " + commandInput.TemplateParamInputFormat("--alias").Bold().Red());
                return New3CommandStatus.NotFound;
            }

            bool hasInvalidParameters = false;
            IReadOnlyCollection<ITemplateMatchInfo> templatesForDisplay = listingTemplateResolutionResult.ExactMatchedTemplates;
            GetParametersInvalidForTemplatesInList(templatesForDisplay, out IReadOnlyList<string> invalidForAllTemplates, out IReadOnlyList<string> invalidForSomeTemplates);
            if (invalidForAllTemplates.Any() || invalidForSomeTemplates.Any())
            {
                hasInvalidParameters = true;
                DisplayInvalidParameters(invalidForAllTemplates);
                DisplayParametersInvalidForSomeTemplates(invalidForSomeTemplates, LocalizableStrings.PartialTemplateMatchSwitchesNotValidForAllMatches);
            }

            if (listingTemplateResolutionResult.HasExactMatches)
            {
                ShowTemplatesFoundMessage(commandInput);
                DisplayTemplateList(listingTemplateResolutionResult.ExactMatchedTemplateGroups, commandInput);
            }
            else
            {
                ShowContextAndTemplateNameMismatchHelp(listingTemplateResolutionResult, commandInput);
            }

            if (!commandInput.IsListFlagSpecified)
            {
                TemplateUsageHelp.ShowInvocationExamples(listingTemplateResolutionResult, _hostSpecificDataLoader, commandInput.CommandName);
            }

            if (hasInvalidParameters)
            {
                return New3CommandStatus.NotFound;
            }
            else
            {
                return listingTemplateResolutionResult.HasExactMatches ? New3CommandStatus.Success : New3CommandStatus.NotFound;
            }
        }

        /// <summary>
        /// Displays the help in case <paramref name="commandInput"/> contains invalid parameters for resolved <paramref name="unambiguousTemplateGroup"/>.
        /// </summary>
        /// <param name="unambiguousTemplateGroup">the unambigious template group to use based on the command input.</param>
        /// <param name="commandInput">the command input.</param>
        /// <returns><see cref="New3CommandStatus.InvalidParamValues"/>.</returns>
        /// <exception cref="ArgumentNullException">when <paramref name="unambiguousTemplateGroup"/>is <see langword="null" />.</exception>
        /// <exception cref="ArgumentNullException">when <paramref name="commandInput"/>is <see langword="null" />.</exception>
        private static New3CommandStatus DisplayInvalidParameterError(TemplateGroup unambiguousTemplateGroup, INewCommandInput commandInput)
        {
            _ = unambiguousTemplateGroup ?? throw new ArgumentNullException(paramName: nameof(unambiguousTemplateGroup));
            _ = unambiguousTemplateGroup ?? throw new ArgumentNullException(paramName: nameof(commandInput));

            var invalidParameters = unambiguousTemplateGroup.GetInvalidParameterList();
            if (invalidParameters.Any())
            {
                Reporter.Error.WriteLine(InvalidParameterInfo.InvalidParameterListToString(invalidParameters, unambiguousTemplateGroup).Bold().Red());
            }

            if (unambiguousTemplateGroup.ShortNames.Any())
            {
                Reporter.Error.WriteLine(
                        string.Format(
                            LocalizableStrings.InvalidParameterTemplateHint,
                            GetTemplateHelpCommand(commandInput.CommandName, unambiguousTemplateGroup.ShortNames[0])).Bold().Red());
            }
            return New3CommandStatus.InvalidParamValues;
        }

        /// <summary>
        /// Displays the help when <paramref name="unambiguousTemplateGroup"/> contains the invokable templates with ambiguous precedence.
        /// </summary>
        /// <param name="unambiguousTemplateGroup">resolved unambiguous template group to use based on the command input.</param>
        /// <param name="commandInput">the command input.</param>
        /// <param name="cancellationToken">a cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">when <paramref name="unambiguousTemplateGroup"/> is <see langword="null" />.</exception>
        /// <exception cref="ArgumentNullException">when <paramref name="commandInput"/> is <see langword="null" />.</exception>
        private async Task<New3CommandStatus> DisplayAmbiguousPrecedenceErrorAsync(
            TemplateGroup unambiguousTemplateGroup,
            INewCommandInput commandInput,
            CancellationToken cancellationToken)
        {
            _ = unambiguousTemplateGroup ?? throw new ArgumentNullException(paramName: nameof(unambiguousTemplateGroup));
            _ = unambiguousTemplateGroup ?? throw new ArgumentNullException(paramName: nameof(commandInput));

            Reporter.Error.WriteLine(LocalizableStrings.AmbiguousTemplatesHeader.Bold().Red());
            List<AmbiguousTemplateDetails> ambiguousTemplateDetails = new List<AmbiguousTemplateDetails>();
            foreach (ITemplateMatchInfo template in unambiguousTemplateGroup.GetHighestPrecedenceInvokableTemplates(true))
            {
                ambiguousTemplateDetails.Add(new AmbiguousTemplateDetails
                {
                    TemplateIdentity = template.Info.Identity,
                    TemplateName = template.Info.Name,
                    TemplateShortNames = template.Info.ShortNameList,
                    TemplateLanguage = template.Info.GetLanguage() ?? string.Empty,
                    TemplatePrecedence = template.Info.Precedence,
                    TemplateAuthor = template.Info.Author ?? string.Empty,
                    TemplatePackage = await _templatePackageManager.GetTemplatePackageAsync(template.Info, cancellationToken).ConfigureAwait(false) as IManagedTemplatePackage
                });
            }

            HelpFormatter<AmbiguousTemplateDetails> formatter =
                HelpFormatter
                    .For(
                        _engineEnvironmentSettings,
                        commandInput,
                        ambiguousTemplateDetails,
                        columnPadding: 2,
                        headerSeparator: '-',
                        blankLineBetweenRows: false)
                    .DefineColumn(t => t.TemplateIdentity, out object identityColumn, LocalizableStrings.ColumnNameIdentity, showAlways: true)
                    .DefineColumn(t => t.TemplateName, LocalizableStrings.ColumnNameTemplateName, shrinkIfNeeded: true, minWidth: 15, showAlways: true)
                    .DefineColumn(t => string.Join(",", t.TemplateShortNames), LocalizableStrings.ColumnNameShortName, showAlways: true)
                    .DefineColumn(t => t.TemplateLanguage, LocalizableStrings.ColumnNameLanguage, showAlways: true)
                    .DefineColumn(t => t.TemplatePrecedence.ToString(), out object prcedenceColumn, LocalizableStrings.ColumnNamePrecedence, showAlways: true)
                    .DefineColumn(t => t.TemplateAuthor, LocalizableStrings.ColumnNameAuthor, showAlways: true, shrinkIfNeeded: true, minWidth: 10)
                    .DefineColumn(t => t.TemplatePackage != null ? t.TemplatePackage.Identifier : string.Empty, LocalizableStrings.ColumnNamePackage, showAlways: true)
                    .OrderBy(identityColumn, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(prcedenceColumn, new NullOrEmptyIsLastStringComparer());
            Reporter.Error.WriteLine(formatter.Layout().Bold().Red());

            string hintMessage = LocalizableStrings.AmbiguousTemplatesMultiplePackagesHint;
            if (unambiguousTemplateGroup.Templates.AllAreTheSame(t => t.Info.MountPointUri))
            {
                IManagedTemplatePackage? templatePackage = await _templatePackageManager.GetTemplatePackageAsync(
                    unambiguousTemplateGroup.Templates.First().Info, cancellationToken).ConfigureAwait(false) as IManagedTemplatePackage;
                if (templatePackage != null)
                {
                    hintMessage = string.Format(LocalizableStrings.AmbiguousTemplatesSamePackageHint, templatePackage.Identifier);
                }
            }
            Reporter.Error.WriteLine(hintMessage.Bold().Red());
            return New3CommandStatus.NotFound;
        }

        private void DisplayTemplateList(
            IReadOnlyCollection<TemplateGroupTableRow> groupsForDisplay,
            INewCommandInput commandInput,
            bool useErrorOutput = false)
        {
            HelpFormatter<TemplateGroupTableRow> formatter =
                HelpFormatter
                    .For(
                        _engineEnvironmentSettings,
                        commandInput,
                        groupsForDisplay,
                        columnPadding: 2,
                        headerSeparator: '-',
                        blankLineBetweenRows: false)
                    .DefineColumn(t => t.Name, out object nameColumn, LocalizableStrings.ColumnNameTemplateName, shrinkIfNeeded: true, minWidth: 15, showAlways: true)
                    .DefineColumn(t => t.ShortNames, LocalizableStrings.ColumnNameShortName, showAlways: true)
                    .DefineColumn(t => t.Languages, out object languageColumn, LocalizableStrings.ColumnNameLanguage, NewCommandInputCli.LanguageColumnFilter, defaultColumn: true)
                    .DefineColumn(t => t.Type, LocalizableStrings.ColumnNameType, NewCommandInputCli.TypeColumnFilter, defaultColumn: false)
                    .DefineColumn(t => t.Author, LocalizableStrings.ColumnNameAuthor, NewCommandInputCli.AuthorColumnFilter, defaultColumn: false, shrinkIfNeeded: true, minWidth: 10)
                    .DefineColumn(t => t.Classifications, out object tagsColumn, LocalizableStrings.ColumnNameTags, NewCommandInputCli.TagsColumnFilter, defaultColumn: true)
                    .OrderBy(nameColumn, StringComparer.OrdinalIgnoreCase);

            Reporter reporter = useErrorOutput ? Reporter.Error : Reporter.Output;
            reporter.WriteLine(formatter.Layout());
        }

#pragma warning disable SA1204 // Static elements should appear before instance elements
        private static void DisplayParametersInvalidForSomeTemplates(IReadOnlyList<string> invalidParams, string messageHeader)
#pragma warning restore SA1204 // Static elements should appear before instance elements
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

        private static void ShowContextAndTemplateNameMismatchHelp(TemplateListResolutionResult templateResolutionResult, INewCommandInput commandInput)
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
                        templateResolutionResult.PartiallyMatchedTemplateGroups.Count,
                        GetPartialMatchReason(templateResolutionResult, commandInput))
                    .Bold().Red());
            }

            if (!commandInput.IsListFlagSpecified)
            {
                // To list installed templates, run 'dotnet {0} --list'.
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.ListTemplatesCommand, commandInput.ListCommandExample()).Bold().Red());
            }

            // To search for the templates on NuGet.org, run 'dotnet {0} <template name> --search'.
            Reporter.Error.WriteLine(string.Format(LocalizableStrings.SearchTemplatesCommand, commandInput.SearchCommandExample(commandInput.TemplateName)).Bold().Red());
            Reporter.Error.WriteLine();
        }

        /// <summary>
        /// Returns the help command for given command name and short name.
        /// </summary>
        private static string GetTemplateHelpCommand(string commandName, string shortName)
        {
            if (string.IsNullOrWhiteSpace(commandName))
            {
                throw new ArgumentException($"{nameof(commandName)} should not be null or empty", nameof(commandName));
            }
            if (string.IsNullOrWhiteSpace(shortName))
            {
                throw new ArgumentException($"{nameof(shortName)} should not be null or empty", nameof(shortName));
            }

            return $"dotnet {commandName} {shortName} --help";
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

        private static string GetPartialMatchReason(TemplateListResolutionResult templateResolutionResult, INewCommandInput commandInput)
        {
            string separator = ", ";
            return string.Join(
                separator,
                SupportedFilterOptions.SupportedListFilters
                .OfType<TemplateFilterOption>()
                .Where(filter => filter.IsFilterSet(commandInput) && filter.MismatchCriteria(templateResolutionResult))
                .Select(filter => $"{filter.Name}='{filter.FilterValue(commandInput)}'"));
        }

        private struct AmbiguousTemplateDetails
        {
            internal string TemplateIdentity;
            internal string TemplateName;
            internal IReadOnlyList<string> TemplateShortNames;
            internal string TemplateLanguage;
            internal int TemplatePrecedence;
            internal string TemplateAuthor;
            internal IManagedTemplatePackage? TemplatePackage;
        }
    }
}
