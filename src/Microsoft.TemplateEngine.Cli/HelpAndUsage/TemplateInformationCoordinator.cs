// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
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
                        string.Format(LocalizableStrings.NoTemplatesMatchingInputParameters, GetInputParametersString(resolutionResult.Resolver.Filters, commandInput)).Bold().Red());
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.ListTemplatesCommand, commandInput.ListCommandExample()).Bold().Red());
                    // To search for the templates on NuGet.org, run 'dotnet {0} <template name> --search'.
                    Reporter.Error.WriteLine(LocalizableStrings.SearchTemplatesCommand.Bold().Red());
                    Reporter.Error.WriteCommand(commandInput.SearchCommandExample(commandInput.TemplateName).Bold().Red());
                    Reporter.Error.WriteLine();
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
                case TemplateResolutionResult.Status.AmbiguousTemplateChoice:
                    if (resolutionResult.UnambiguousTemplateGroup == null)
                    {
                        throw new ArgumentException($"{nameof(resolutionResult.UnambiguousTemplateGroup)} should not be null if {nameof(resolutionResult.ResolutionStatus)} is {nameof(TemplateResolutionResult.Status.AmbiguousTemplateChoice)}");
                    }
                    if (resolutionResult.UnambiguousTemplateGroupMatchInfo == null)
                    {
                        throw new ArgumentException($"{nameof(resolutionResult.UnambiguousTemplateGroupMatchInfo)} should not be null if {nameof(resolutionResult.ResolutionStatus)} is {nameof(TemplateResolutionResult.Status.AmbiguousTemplateChoice)}");
                    }
                    Reporter.Verbose.WriteLine(LocalizableStrings.Authoring_AmbiguousBestPrecedence);
                    return DisplayAmbiguousPrecedenceErrorAsync(resolutionResult.UnambiguousTemplateGroupMatchInfo, commandInput, cancellationToken);
                case TemplateResolutionResult.Status.InvalidParameter:
                    if (resolutionResult.UnambiguousTemplateGroup == null)
                    {
                        throw new ArgumentException($"{nameof(resolutionResult.UnambiguousTemplateGroup)} should not be null if {nameof(resolutionResult.ResolutionStatus)} is {nameof(TemplateResolutionResult.Status.InvalidParameter)}");
                    }
                    if (resolutionResult.UnambiguousTemplateGroupMatchInfo == null)
                    {
                        throw new ArgumentException($"{nameof(resolutionResult.UnambiguousTemplateGroupMatchInfo)} should not be null if {nameof(resolutionResult.ResolutionStatus)} is {nameof(TemplateResolutionResult.Status.InvalidParameter)}");
                    }
                    return Task.FromResult(DisplayInvalidParameterError(
                        resolutionResult.UnambiguousTemplateGroupMatchInfo,
                        commandInput));
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
            IEnumerable<TemplateGroup> templateGroups,
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
                Reporter.Error.WriteLine(LocalizableStrings.InvalidCommandOptions.Bold().Red());
                foreach (string flag in invalidParams)
                {
                    Reporter.Error.WriteLine($"  {flag}".Bold().Red());
                }
            }
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
            ValidateRemainingParameters(commandInput, out IReadOnlyList<string> invalidParams);
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
        /// Handles help display for the template (dotnet new3 template-name --help).
        /// </summary>
        /// <param name="commandInput">user command input.</param>
        /// <param name="cancellationToken">cancellation token.</param>
        /// <returns><see cref="New3CommandStatus"/> for operation.</returns>
        internal async Task<New3CommandStatus> DisplayTemplateHelpAsync(
            INewCommandInput commandInput,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HelpTemplateResolver resolver = new HelpTemplateResolver(_templatePackageManager, _hostSpecificDataLoader);

            TemplateResolutionResult resolutionResult = await resolver.ResolveTemplatesAsync(commandInput, _defaultLanguage, cancellationToken).ConfigureAwait(false);

            if (resolutionResult.GroupResolutionStatus == TemplateResolutionResult.TemplateGroupStatus.SingleMatch
                && resolutionResult.ResolutionStatus != TemplateResolutionResult.Status.AmbiguousLanguageChoice
                && resolutionResult.UnambiguousTemplateGroupMatchInfo!.TemplateMatchInfosWithMatchingParametersForPreferredLanguage.Any())
            {
                return await TemplateDetailsDisplay.ShowTemplateGroupHelpAsync(resolutionResult.UnambiguousTemplateGroupMatchInfo, _engineEnvironmentSettings, commandInput, _hostSpecificDataLoader, _templateCreator, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                return await CoordinateAmbiguousTemplateResolutionDisplayAsync(resolutionResult, commandInput, cancellationToken).ConfigureAwait(false);
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
            ListTemplateResolver resolver = new ListTemplateResolver(_templatePackageManager, _hostSpecificDataLoader);
            TemplateResolutionResult resolutionResult = await resolver.ResolveTemplatesAsync(commandInput, _defaultLanguage, cancellationToken).ConfigureAwait(false);

            IReadOnlyDictionary<string, string?>? appliedParameterMatches = resolutionResult.GetAllMatchedParametersList();
            if (resolutionResult.TemplateGroupsWithMatchingTemplateInfoAndParameters.Any())
            {
                Reporter.Output.WriteLine(
                    string.Format(
                        LocalizableStrings.TemplatesFoundMatchingInputParameters,
                        GetInputParametersString(resolutionResult.Resolver.Filters, commandInput, appliedParameterMatches)));
                Reporter.Output.WriteLine();
                DisplayTemplateList(resolutionResult.TemplateGroupsWithMatchingTemplateInfoAndParameters, commandInput);
                return New3CommandStatus.Success;
            }
            else
            {
                // No templates found matching the following input parameter(s): {0}.
                Reporter.Error.WriteLine(
                    string.Format(
                        LocalizableStrings.NoTemplatesMatchingInputParameters,
                        GetInputParametersString(ListTemplateResolver.SupportedFilters, commandInput, appliedParameterMatches))
                    .Bold().Red());

                if (resolutionResult.HasTemplateGroupMatches)
                {
                    // {0} template(s) partially matched, but failed on {1}.
                    Reporter.Error.WriteLine(
                        string.Format(
                            LocalizableStrings.TemplatesNotValidGivenTheSpecifiedFilter,
                            resolutionResult.TemplateGroups.Count(),
                            GetPartialMatchReason(resolutionResult, commandInput, appliedParameterMatches))
                        .Bold().Red());
                }

                // To search for the templates on NuGet.org, run:
                Reporter.Error.WriteLine(LocalizableStrings.SearchTemplatesCommand);
                if (string.IsNullOrWhiteSpace(commandInput.TemplateName))
                {
                    Reporter.Error.WriteCommand(commandInput.SearchCommandExample(usePlaceholder: true));
                }
                else
                {
                    Reporter.Error.WriteCommand(commandInput.SearchCommandExample(commandInput.TemplateName));
                }
                Reporter.Error.WriteLine();
                return New3CommandStatus.NotFound;
            }
        }

        /// <summary>
        /// Handles display for dotnet new command without parameters.
        /// </summary>
        /// <param name="commandInput">user command input.</param>
        /// <param name="cancellationToken">cancellation token.</param>
        /// <returns></returns>
        internal async Task<New3CommandStatus> DisplayCommandDescriptionAsync(
            INewCommandInput commandInput,
            CancellationToken cancellationToken)
        {
            Reporter.Output.WriteLine(string.Format(
                LocalizableStrings.TemplateInformationCoordinator_DotnetNew_Description,
                commandInput.New3CommandExample()));
            Reporter.Output.WriteLine();

            Reporter.Output.WriteLine(string.Format(
              LocalizableStrings.TemplateInformationCoordinator_DotnetNew_TemplatesHeader,
              commandInput.New3CommandExample()));
            await ShowCuratedListAsync(commandInput, cancellationToken).ConfigureAwait(false);

            Reporter.Output.WriteLine(LocalizableStrings.TemplateInformationCoordinator_DotnetNew_ExampleHeader);
            Reporter.Output.WriteCommand(commandInput.InstantiateTemplateExample("app"));
            Reporter.Output.WriteLine();

            Reporter.Output.WriteLine(LocalizableStrings.TemplateInformationCoordinator_DotnetNew_DisplayOptionsHint);
            Reporter.Output.WriteCommand(commandInput.HelpCommandExample("app"));

            Reporter.Output.WriteLine(LocalizableStrings.TemplateInformationCoordinator_DotnetNew_ListTemplatesHint);
            Reporter.Output.WriteCommand(commandInput.ListCommandExample());

            Reporter.Output.WriteLine(LocalizableStrings.TemplateInformationCoordinator_DotnetNew_SearchTemplatesHint);
            Reporter.Output.WriteCommand(commandInput.SearchCommandExample("ap"));

            Reporter.Output.WriteLine();

            return New3CommandStatus.Success;
        }

        // This version relies on the commandInput being in the context desired - so the most recent parse would have to have been
        // for what wants to be validated, either:
        //  - not in the context of any template
        //  - in the context of a specific template.
        private static bool ValidateRemainingParameters(INewCommandInput commandInput, out IReadOnlyList<string> invalidParams)
        {
            List<string> badParams = new List<string>();

            if (commandInput.RemainingParameters.Any())
            {
                foreach (string flag in commandInput.RemainingParameters)
                {
                    badParams.Add(flag);
                }
            }

            invalidParams = badParams;
            return !invalidParams.Any();
        }

        /// <summary>
        /// Displays the help in case <paramref name="commandInput"/> contains invalid parameters for resolved <paramref name="templateGroupMatchInfo"/>.
        /// </summary>
        /// <param name="templateGroupMatchInfo">the template group to use based on the command input.</param>
        /// <param name="commandInput">the command input.</param>
        /// <returns><see cref="New3CommandStatus.InvalidParamValues"/>.</returns>
        private static New3CommandStatus DisplayInvalidParameterError(TemplateGroupMatchInfo templateGroupMatchInfo, INewCommandInput commandInput)
        {
            _ = templateGroupMatchInfo ?? throw new ArgumentNullException(paramName: nameof(templateGroupMatchInfo));
            _ = commandInput ?? throw new ArgumentNullException(paramName: nameof(commandInput));

            var invalidParameters = templateGroupMatchInfo.GetInvalidParameterList();
            if (invalidParameters.Any())
            {
                Reporter.Error.WriteLine(InvalidParameterInfo.InvalidParameterListToString(invalidParameters, templateGroupMatchInfo).Bold().Red());
            }

            if (templateGroupMatchInfo.GroupInfo.ShortNames.Any())
            {
                Reporter.Error.WriteLine(LocalizableStrings.InvalidParameterTemplateHint.Bold().Red());
                Reporter.Error.WriteCommand(commandInput.HelpCommandExample(templateGroupMatchInfo.GroupInfo.ShortNames[0]).Bold().Red());
            }
            return New3CommandStatus.InvalidParamValues;
        }

        /// <summary>
        /// Displays curated list of templates for dotnet new command.
        /// </summary>
        private async Task ShowCuratedListAsync(INewCommandInput commandInput, CancellationToken cancellationToken)
        {
            string[] curatedGroupIdentityList = new[]
            {
                "Microsoft.Common.Library", //classlib
                "Microsoft.Common.Console", //console
                "Microsoft.Common.App", //app
                "Microsoft.Common.WPF", //wpf
                "Microsoft.Common.WinForms", //winforms
                "Microsoft.Web.Blazor.Wasm", //blazorwasm
                "Microsoft.Web.RazorPages" //webapp
            };

            IReadOnlyList<ITemplateInfo> templates = await _templatePackageManager.GetTemplatesAsync(cancellationToken).ConfigureAwait(false);
            IEnumerable<ITemplateInfo> filteredTemplates = templates.Where(t => curatedGroupIdentityList.Contains(t.GroupIdentity, StringComparer.OrdinalIgnoreCase));
            DisplayTemplateList(filteredTemplates, commandInput);
        }

        /// <summary>
        /// Displays the help when <paramref name="templateGroupMatchInfo"/> contains the templates with ambiguous precedence.
        /// </summary>
        /// <param name="templateGroupMatchInfo">resolved template group to use based on the command input.</param>
        /// <param name="commandInput">the command input.</param>
        /// <param name="cancellationToken">a cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">when <paramref name="templateGroupMatchInfo"/> is <see langword="null" />.</exception>
        /// <exception cref="ArgumentNullException">when <paramref name="commandInput"/> is <see langword="null" />.</exception>
        private async Task<New3CommandStatus> DisplayAmbiguousPrecedenceErrorAsync(
            TemplateGroupMatchInfo templateGroupMatchInfo,
            INewCommandInput commandInput,
            CancellationToken cancellationToken)
        {
            _ = templateGroupMatchInfo ?? throw new ArgumentNullException(paramName: nameof(templateGroupMatchInfo));
            _ = templateGroupMatchInfo ?? throw new ArgumentNullException(paramName: nameof(commandInput));

            Reporter.Error.WriteLine(LocalizableStrings.AmbiguousTemplatesHeader.Bold().Red());
            List<AmbiguousTemplateDetails> ambiguousTemplateDetails = new List<AmbiguousTemplateDetails>();
            foreach (ITemplateInfo template in templateGroupMatchInfo.GetHighestPrecedenceTemplates())
            {
                ambiguousTemplateDetails.Add(new AmbiguousTemplateDetails
                {
                    TemplateIdentity = template.Identity,
                    TemplateName = template.Name,
                    TemplateShortNames = template.ShortNameList,
                    TemplateLanguage = template.GetLanguage() ?? string.Empty,
                    TemplatePrecedence = template.Precedence,
                    TemplateAuthor = template.Author ?? string.Empty,
                    TemplatePackage = await _templatePackageManager.GetTemplatePackageAsync(template, cancellationToken).ConfigureAwait(false) as IManagedTemplatePackage
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
            if (templateGroupMatchInfo.GroupInfo.Templates.AllAreTheSame(t => t.MountPointUri))
            {
                IManagedTemplatePackage? templatePackage = await _templatePackageManager.GetTemplatePackageAsync(
                    templateGroupMatchInfo.GroupInfo.Templates[0], cancellationToken).ConfigureAwait(false) as IManagedTemplatePackage;
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
                    .DefineColumn(t => t.Languages, out object languageColumn, LocalizableStrings.ColumnNameLanguage, BaseCommandInput.LanguageColumnFilter, defaultColumn: true)
                    .DefineColumn(t => t.Type, LocalizableStrings.ColumnNameType, BaseCommandInput.TypeColumnFilter, defaultColumn: false)
                    .DefineColumn(t => t.Author, LocalizableStrings.ColumnNameAuthor, BaseCommandInput.AuthorColumnFilter, defaultColumn: false, shrinkIfNeeded: true, minWidth: 10)
                    .DefineColumn(t => t.Classifications, out object tagsColumn, LocalizableStrings.ColumnNameTags, BaseCommandInput.TagsColumnFilter, defaultColumn: true)
                    .OrderBy(nameColumn, StringComparer.OrdinalIgnoreCase);

            Reporter reporter = useErrorOutput ? Reporter.Error : Reporter.Output;
            reporter.WriteLine(formatter.Layout());
        }

#pragma warning disable SA1202 // Elements should be ordered by access
        internal static string GetInputParametersString(IEnumerable<FilterOption> supportedFilters, INewCommandInput commandInput, IReadOnlyDictionary<string, string?>? templateParameters = null)
#pragma warning restore SA1202 // Elements should be ordered by access
        {
            string separator = ", ";
            IEnumerable<string> appliedFilters = supportedFilters
                    .Where(filter => filter.IsFilterSet(commandInput))
                    .Select(filter => $"{filter.Name}='{filter.FilterValue(commandInput)}'");

            IEnumerable<string> appliedTemplateParameters = templateParameters?
                   .Select(param => string.IsNullOrWhiteSpace(param.Value) ? param.Key : $"{param.Key}='{param.Value}'") ?? Array.Empty<string>();

            StringBuilder inputParameters = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(commandInput.TemplateName))
            {
                inputParameters.Append($"'{commandInput.TemplateName}'");
                if (appliedFilters.Any() || appliedTemplateParameters.Any())
                {
                    inputParameters.Append(separator);
                }
            }
            if (appliedFilters.Concat(appliedTemplateParameters).Any())
            {
                inputParameters.Append(string.Join(separator, appliedFilters.Concat(appliedTemplateParameters)));
            }
            return inputParameters.ToString();
        }

        private static string GetPartialMatchReason(TemplateResolutionResult templateResolutionResult, INewCommandInput commandInput, IReadOnlyDictionary<string, string?>? templateParameters = null)
        {
            string separator = ", ";

            IEnumerable<string> appliedFilters = templateResolutionResult.Resolver.Filters
                    .OfType<TemplateFilterOption>()
                    .Where(filter => filter.IsFilterSet(commandInput) && filter.MismatchCriteria(templateResolutionResult))
                    .Select(filter => $"{filter.Name}='{filter.FilterValue(commandInput)}'");

            IEnumerable<string> appliedTemplateParameters = templateParameters?
                   .Where(parameter =>
                        templateResolutionResult.IsParameterMismatchReason(parameter.Key))
                   .Select(param => string.IsNullOrWhiteSpace(param.Value) ? param.Key : $"{param.Key}='{param.Value}'") ?? Array.Empty<string>();

            StringBuilder inputParameters = new StringBuilder();
            if (appliedFilters.Concat(appliedTemplateParameters).Any())
            {
                inputParameters.Append(string.Join(separator, appliedFilters.Concat(appliedTemplateParameters)));
            }
            return inputParameters.ToString();
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
