// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Cli.TabularOutput;
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

        private readonly ITabularOutputSettings _defaultTabularOutputSettings;

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

            _defaultTabularOutputSettings = new CliTabularOutputSettings(engineEnvironmentSettings.Environment);
        }

        /// <summary>
        /// Displays the help in case it is not possible to resolve template to use based on user input.
        /// Used for template instantiation and template help.
        /// </summary>
        /// <param name="resolutionResult">template resolution result.</param>
        /// <param name="commandInput">command input used in CLI.</param>
        /// <param name="cancellationToken">cancellation token.</param>
        /// <returns></returns>
        internal Task<NewCommandStatus> CoordinateAmbiguousTemplateResolutionDisplayAsync(
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
                    Reporter.Error.WriteLine();

                    Reporter.Error.WriteLine(LocalizableStrings.ListTemplatesCommand);
                    Reporter.Error.WriteCommand(commandInput.ListCommandExample());

                    Reporter.Error.WriteLine(LocalizableStrings.SearchTemplatesCommand);
                    Reporter.Error.WriteCommand(commandInput.SearchCommandExample(commandInput.TemplateName));
                    Reporter.Error.WriteLine();
                    return Task.FromResult(NewCommandStatus.NotFound);
                case TemplateResolutionResult.Status.AmbiguousLanguageChoice:
                    Reporter.Error.WriteLine(LocalizableStrings.AmbiguousTemplateGroupListHeader.Bold().Red());
                    DisplayTemplateList(resolutionResult.TemplateGroups, _defaultTabularOutputSettings, useErrorOutput: true);
                    Reporter.Error.WriteLine(LocalizableStrings.AmbiguousLanguageHint.Bold().Red());
                    return Task.FromResult(NewCommandStatus.NotFound);
                case TemplateResolutionResult.Status.AmbiguousTemplateGroupChoice:
                    Reporter.Error.WriteLine(LocalizableStrings.AmbiguousTemplateGroupListHeader.Bold().Red());
                    DisplayTemplateList(resolutionResult.TemplateGroups, _defaultTabularOutputSettings, useErrorOutput: true);
                    //TODO: https://github.com/dotnet/templating/issues/3275
                    //revise error handling: this message is not the best CTA
                    //Reporter.Error.WriteLine(LocalizableStrings.AmbiguousTemplateGroupListHint.Bold().Red());
                    return Task.FromResult(NewCommandStatus.NotFound);
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
            return Task.FromResult(NewCommandStatus.CreateFailed);
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
            ITabularOutputSettings helpFormatterSettings,
            string? selectedLanguage = null,
            bool useErrorOutput = false)
        {
            IReadOnlyCollection<TemplateGroupTableRow> groupsForDisplay = TemplateGroupDisplay.GetTemplateGroupsForListDisplay(
                templateGroups,
                selectedLanguage,
                _defaultLanguage,
                _engineEnvironmentSettings.Environment);
            DisplayTemplateList(groupsForDisplay, helpFormatterSettings, useErrorOutput);
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
            ITabularOutputSettings helpFormatterSettings,
            string? selectedLanguage = null,
            bool useErrorOutput = false)
        {
            IReadOnlyCollection<TemplateGroupTableRow> groupsForDisplay = TemplateGroupDisplay.GetTemplateGroupsForListDisplay(
                templates,
                selectedLanguage,
                _defaultLanguage,
                _engineEnvironmentSettings.Environment);
            DisplayTemplateList(groupsForDisplay, helpFormatterSettings, useErrorOutput);
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

        /// <summary>
        /// Handles help display for the template (dotnet new3 template-name --help).
        /// </summary>
        /// <param name="commandInput">user command input.</param>
        /// <param name="cancellationToken">cancellation token.</param>
        /// <returns><see cref="NewCommandStatus"/> for operation.</returns>
        internal async Task<NewCommandStatus> DisplayTemplateHelpAsync(
            INewCommandInput commandInput,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HelpTemplateResolver resolver = new HelpTemplateResolver(_templatePackageManager, _hostSpecificDataLoader);

            TemplateResolutionResult resolutionResult = await resolver.ResolveTemplatesAsync(commandInput, _defaultLanguage, cancellationToken).ConfigureAwait(false);

            if (resolutionResult.GroupResolutionStatus == TemplateResolutionResult.TemplateGroupStatus.SingleMatch
                && resolutionResult.ResolutionStatus != TemplateResolutionResult.Status.AmbiguousLanguageChoice
                && resolutionResult.UnambiguousTemplateGroup != null
                && resolutionResult.UnambiguousTemplateGroupMatchInfo != null
                && resolutionResult.UnambiguousTemplateGroupMatchInfo.TemplateMatchInfosWithMatchingParametersForPreferredLanguage.Any())
            {
                NewCommandStatus result = await TemplateDetailsDisplay.ShowTemplateGroupHelpAsync(
                    resolutionResult.UnambiguousTemplateGroupMatchInfo,
                    _engineEnvironmentSettings,
                    commandInput,
                    _hostSpecificDataLoader,
                    _templateCreator,
                    cancellationToken).ConfigureAwait(false);

                if (result == NewCommandStatus.Success)
                {
                    DisplayHintForOtherLanguages(commandInput, resolutionResult);
                }
                return result;
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
        internal async Task<NewCommandStatus> DisplayTemplateGroupListAsync(
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
                DisplayTemplateList(resolutionResult.TemplateGroupsWithMatchingTemplateInfoAndParameters, _defaultTabularOutputSettings, selectedLanguage: commandInput.Language);
                return NewCommandStatus.Success;
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

                Reporter.Error.WriteLine();
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
                return NewCommandStatus.NotFound;
            }
        }

        /// <summary>
        /// Handles display for dotnet new command without parameters.
        /// </summary>
        /// <param name="commandInput">user command input.</param>
        /// <param name="cancellationToken">cancellation token.</param>
        /// <returns></returns>
        internal async Task<NewCommandStatus> DisplayCommandDescriptionAsync(
            INewCommandInput commandInput,
            CancellationToken cancellationToken)
        {
            IEnumerable<ITemplateInfo> curatedTemplates = await GetCuratedListAsync(commandInput, cancellationToken).ConfigureAwait(false);

            Reporter.Output.WriteLine(string.Format(
                LocalizableStrings.TemplateInformationCoordinator_DotnetNew_Description,
                commandInput.New3CommandExample()));
            Reporter.Output.WriteLine();

            Reporter.Output.WriteLine(string.Format(
              LocalizableStrings.TemplateInformationCoordinator_DotnetNew_TemplatesHeader,
              commandInput.New3CommandExample()));
            DisplayTemplateList(curatedTemplates, _defaultTabularOutputSettings);

            Reporter.Output.WriteLine(LocalizableStrings.TemplateInformationCoordinator_DotnetNew_ExampleHeader);
            Reporter.Output.WriteCommand(commandInput.InstantiateTemplateExample("console"));
            Reporter.Output.WriteLine();

            Reporter.Output.WriteLine(LocalizableStrings.TemplateInformationCoordinator_DotnetNew_DisplayOptionsHint);
            Reporter.Output.WriteCommand(commandInput.HelpCommandExample("console"));

            Reporter.Output.WriteLine(LocalizableStrings.TemplateInformationCoordinator_DotnetNew_ListTemplatesHint);
            Reporter.Output.WriteCommand(commandInput.ListCommandExample());

            Reporter.Output.WriteLine(LocalizableStrings.TemplateInformationCoordinator_DotnetNew_SearchTemplatesHint);
            Reporter.Output.WriteCommand(commandInput.SearchCommandExample("web"));

            Reporter.Output.WriteLine();

            return NewCommandStatus.Success;
        }

        /// <summary>
        /// Displays the help in case <paramref name="commandInput"/> contains invalid parameters for resolved <paramref name="templateGroupMatchInfo"/>.
        /// </summary>
        /// <param name="templateGroupMatchInfo">the template group to use based on the command input.</param>
        /// <param name="commandInput">the command input.</param>
        /// <returns><see cref="NewCommandStatus.InvalidParamValues"/>.</returns>
        private static NewCommandStatus DisplayInvalidParameterError(TemplateGroupMatchInfo templateGroupMatchInfo, INewCommandInput commandInput)
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
                Reporter.Error.WriteLine(LocalizableStrings.InvalidParameterTemplateHint);
                Reporter.Error.WriteCommand(commandInput.HelpCommandExample(templateGroupMatchInfo.GroupInfo.ShortNames[0]));
            }
            return NewCommandStatus.InvalidParamValues;
        }

        private static void DisplayHintForOtherLanguages(INewCommandInput commandInput, TemplateResolutionResult resolutionResult)
        {
            if (resolutionResult.UnambiguousTemplateGroup == null || resolutionResult.UnambiguousTemplateGroupMatchInfo == null)
            {
                return;
            }

            if (resolutionResult.UnambiguousTemplateGroup.Languages.Count <= 1)
            {
                return;
            }

            string? preferredLanguage =
                resolutionResult.UnambiguousTemplateGroupMatchInfo
                    .TemplatesWithMatchingParametersForPreferredLanguage
                    .OrderBy(t => t.GetLanguage(), StringComparer.OrdinalIgnoreCase)
                    .First().GetLanguage();

            List<string> supportedLanguages = new List<string>();
            foreach (string? language in resolutionResult.UnambiguousTemplateGroup.Languages)
            {
                if (string.IsNullOrWhiteSpace(language))
                {
                    continue;
                }
                if (!language.Equals(preferredLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    supportedLanguages.Add(language);
                }
            }
            if (supportedLanguages.Any())
            {
                supportedLanguages.Sort(StringComparer.OrdinalIgnoreCase);
                string supportedLanguagesStr = string.Join(", ", supportedLanguages);
                Reporter.Output.WriteLine(string.Format(LocalizableStrings.TemplateInformationCoordinator_TemplateHelp_Info_HelpForOtherLanguagesHint, supportedLanguagesStr));
                Reporter.Output.WriteCommand(
                    commandInput.HelpCommandExample(resolutionResult.UnambiguousTemplateGroup.ShortNames[0], supportedLanguages.First()));
                Reporter.Output.WriteLine();
            }
        }

        /// <summary>
        /// Displays curated list of templates for dotnet new command.
        /// </summary>
        private async Task<IEnumerable<ITemplateInfo>> GetCuratedListAsync(INewCommandInput commandInput, CancellationToken cancellationToken)
        {
            string[] curatedGroupIdentityList = new[]
            {
                "Microsoft.Common.Library", //classlib
                "Microsoft.Common.Console", //console
                "Microsoft.Common.WPF", //wpf
                "Microsoft.Common.WinForms", //winforms
                "Microsoft.Web.Blazor.Server", //blazorserver
                "Microsoft.Web.RazorPages" //webapp
            };

            IReadOnlyList<ITemplateInfo> templates = await _templatePackageManager.GetTemplatesAsync(cancellationToken).ConfigureAwait(false);
            return templates.Where(t => curatedGroupIdentityList.Contains(t.GroupIdentity, StringComparer.OrdinalIgnoreCase));
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
        private async Task<NewCommandStatus> DisplayAmbiguousPrecedenceErrorAsync(
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

            TabularOutput<AmbiguousTemplateDetails> formatter =
                TabularOutput.TabularOutput
                    .For(
                        _defaultTabularOutputSettings,
                        ambiguousTemplateDetails)
                    .DefineColumn(t => t.TemplateIdentity, out object identityColumn, LocalizableStrings.ColumnNameIdentity, showAlways: true)
                    .DefineColumn(t => t.TemplateName, LocalizableStrings.ColumnNameTemplateName, shrinkIfNeeded: true, minWidth: 15, showAlways: true)
                    .DefineColumn(t => string.Join(",", t.TemplateShortNames), LocalizableStrings.ColumnNameShortName, showAlways: true)
                    .DefineColumn(t => t.TemplateLanguage, LocalizableStrings.ColumnNameLanguage, showAlways: true)
                    .DefineColumn(t => t.TemplatePrecedence.ToString(), out object prcedenceColumn, LocalizableStrings.ColumnNamePrecedence, showAlways: true)
                    .DefineColumn(t => t.TemplateAuthor, LocalizableStrings.ColumnNameAuthor, showAlways: true, shrinkIfNeeded: true, minWidth: 10)
                    .DefineColumn(t => t.TemplatePackage != null ? t.TemplatePackage.Identifier : string.Empty, LocalizableStrings.ColumnNamePackage, showAlways: true)
                    .OrderBy(identityColumn, StringComparer.CurrentCultureIgnoreCase)
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
            return NewCommandStatus.NotFound;
        }

        private void DisplayTemplateList(
            IReadOnlyCollection<TemplateGroupTableRow> groupsForDisplay,
            ITabularOutputSettings tabularOutputSettings,
            bool useErrorOutput = false)
        {
            TabularOutput<TemplateGroupTableRow> formatter =
                TabularOutput.TabularOutput
                    .For(
                        tabularOutputSettings,
                        groupsForDisplay)
                    .DefineColumn(t => t.Name, out object nameColumn, LocalizableStrings.ColumnNameTemplateName, shrinkIfNeeded: true, minWidth: 15, showAlways: true)
                    .DefineColumn(t => t.ShortNames, LocalizableStrings.ColumnNameShortName, showAlways: true)
                    .DefineColumn(t => t.Languages, out object languageColumn, LocalizableStrings.ColumnNameLanguage, BaseCommandInput.LanguageColumnFilter, defaultColumn: true)
                    .DefineColumn(t => t.Type, LocalizableStrings.ColumnNameType, BaseCommandInput.TypeColumnFilter, defaultColumn: false)
                    .DefineColumn(t => t.Author, LocalizableStrings.ColumnNameAuthor, BaseCommandInput.AuthorColumnFilter, defaultColumn: false, shrinkIfNeeded: true, minWidth: 10)
                    .DefineColumn(t => t.Classifications, out object tagsColumn, LocalizableStrings.ColumnNameTags, BaseCommandInput.TagsColumnFilter, defaultColumn: true)
                    .OrderBy(nameColumn, StringComparer.CurrentCultureIgnoreCase);

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
            string? mainCriteria = commandInput.TemplateName;
            if (commandInput.IsListFlagSpecified)
            {
                mainCriteria = commandInput.ListNameCriteria;
            }
            else if (commandInput.IsSearchFlagSpecified)
            {
                mainCriteria = commandInput.SearchNameCriteria;
            }
            if (!string.IsNullOrWhiteSpace(mainCriteria))
            {
                inputParameters.Append($"'{mainCriteria}'");
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
