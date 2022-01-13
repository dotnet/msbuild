// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Cli.Extensions;
using Microsoft.TemplateEngine.Cli.TabularOutput;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli
{
    internal class TemplateListCoordinator
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;
        private readonly TemplatePackageManager _templatePackageManager;
        private readonly IHostSpecificDataLoader _hostSpecificDataLoader;
        private readonly ITelemetryLogger _telemetryLogger;
        private readonly string? _defaultLanguage;

        internal TemplateListCoordinator(
            IEngineEnvironmentSettings engineEnvironmentSettings,
            TemplatePackageManager templatePackageManager,
            IHostSpecificDataLoader hostSpecificDataLoader,
            ITelemetryLogger telemetryLogger)

        {
            _engineEnvironmentSettings = engineEnvironmentSettings ?? throw new ArgumentNullException(nameof(engineEnvironmentSettings));
            _templatePackageManager = templatePackageManager ?? throw new ArgumentNullException(nameof(templatePackageManager));
            _hostSpecificDataLoader = hostSpecificDataLoader ?? throw new ArgumentNullException(nameof(hostSpecificDataLoader));
            _telemetryLogger = telemetryLogger ?? throw new ArgumentNullException(nameof(telemetryLogger));
            _defaultLanguage = engineEnvironmentSettings.GetDefaultLanguage();
        }

        /// <summary>
        /// Handles template list display (dotnet new3 --list).
        /// </summary>
        /// <param name="args">user command input.</param>
        /// <param name="cancellationToken">cancellation token.</param>
        /// <returns></returns>
        internal async Task<NewCommandStatus> DisplayTemplateGroupListAsync(
            ListCommandArgs args,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ListTemplateResolver resolver = new ListTemplateResolver(_templatePackageManager, _hostSpecificDataLoader);
            TemplateResolutionResult resolutionResult = await resolver.ResolveTemplatesAsync(args, _defaultLanguage, cancellationToken).ConfigureAwait(false);

            //IReadOnlyDictionary<string, string?>? appliedParameterMatches = resolutionResult.GetAllMatchedParametersList();
            if (resolutionResult.TemplateGroupsWithMatchingTemplateInfoAndParameters.Any())
            {
                Reporter.Output.WriteLine(
                    string.Format(
                        LocalizableStrings.TemplatesFoundMatchingInputParameters,
                       GetInputParametersString(args/*, appliedParameterMatches*/)));
                Reporter.Output.WriteLine();

                TabularOutputSettings settings = new TabularOutputSettings(_engineEnvironmentSettings.Environment, args);

                TemplateGroupDisplay.DisplayTemplateList(
                    _engineEnvironmentSettings,
                    resolutionResult.TemplateGroupsWithMatchingTemplateInfoAndParameters,
                    settings,
                    reporter: Reporter.Output,
                    selectedLanguage: args.Language);
                return NewCommandStatus.Success;
            }
            else
            {
                //if there is no criteria and filters it means that dotnet new list was run but there is no templates installed.
                if (args.ListNameCriteria == null && !args.AppliedFilters.Any())
                {
                    //No templates installed.
                    Reporter.Output.WriteLine(LocalizableStrings.NoTemplatesFound);
                    Reporter.Output.WriteLine();
                    // To search for the templates on NuGet.org, run:
                    Reporter.Output.WriteLine(LocalizableStrings.SearchTemplatesCommand);
                    Reporter.Output.WriteCommand(CommandExamples.SearchCommandExample(args.CommandName, usePlaceholder: true));
                    Reporter.Output.WriteLine();
                    return NewCommandStatus.Success;
                }

                // at least one criteria was specified.
                // No templates found matching the following input parameter(s): {0}.
                Reporter.Error.WriteLine(
                    string.Format(
                        LocalizableStrings.NoTemplatesMatchingInputParameters,
                        GetInputParametersString(args/*, appliedParameterMatches*/))
                    .Bold().Red());

                if (resolutionResult.HasTemplateGroupMatches)
                {
                    // {0} template(s) partially matched, but failed on {1}.
                    Reporter.Error.WriteLine(
                        string.Format(
                            LocalizableStrings.TemplatesNotValidGivenTheSpecifiedFilter,
                            resolutionResult.TemplateGroups.Count(),
                            GetPartialMatchReason(resolutionResult, args/*, appliedParameterMatches*/))
                        .Bold().Red());
                }

                Reporter.Error.WriteLine();
                // To search for the templates on NuGet.org, run:
                Reporter.Error.WriteLine(LocalizableStrings.SearchTemplatesCommand);
                if (string.IsNullOrWhiteSpace(args.ListNameCriteria))
                {
                    Reporter.Error.WriteCommand(CommandExamples.SearchCommandExample(args.CommandName, usePlaceholder: true));
                }
                else
                {
                    Reporter.Error.WriteCommand(CommandExamples.SearchCommandExample(args.CommandName, args.ListNameCriteria));
                }
                Reporter.Error.WriteLine();
                return NewCommandStatus.NotFound;
            }
        }

        /// <summary>
        /// Handles display for dotnet new command without parameters.
        /// </summary>
        /// <param name="args">command arguments.</param>
        /// <param name="cancellationToken">cancellation token.</param>
        /// <returns></returns>
        internal async Task<NewCommandStatus> DisplayCommandDescriptionAsync(
            GlobalArgs args,
            CancellationToken cancellationToken)
        {
            IEnumerable<ITemplateInfo> curatedTemplates = await GetCuratedListAsync(cancellationToken).ConfigureAwait(false);

            Reporter.Output.WriteLine(string.Format(
                LocalizableStrings.TemplateInformationCoordinator_DotnetNew_Description,
                CommandExamples.New3CommandExample(args.CommandName)));
            Reporter.Output.WriteLine();

            Reporter.Output.WriteLine(string.Format(
              LocalizableStrings.TemplateInformationCoordinator_DotnetNew_TemplatesHeader,
              CommandExamples.New3CommandExample(args.CommandName)));
            TemplateGroupDisplay.DisplayTemplateList(
                _engineEnvironmentSettings,
                curatedTemplates,
                new TabularOutputSettings(_engineEnvironmentSettings.Environment),
                reporter: Reporter.Output);

            Reporter.Output.WriteLine(LocalizableStrings.TemplateInformationCoordinator_DotnetNew_ExampleHeader);
            Reporter.Output.WriteCommand(CommandExamples.InstantiateTemplateExample(args.CommandName, "console"));
            Reporter.Output.WriteLine();

            Reporter.Output.WriteLine(LocalizableStrings.TemplateInformationCoordinator_DotnetNew_DisplayOptionsHint);
            Reporter.Output.WriteCommand(CommandExamples.HelpCommandExample(args.CommandName, "console"));

            Reporter.Output.WriteLine(LocalizableStrings.TemplateInformationCoordinator_DotnetNew_ListTemplatesHint);
            Reporter.Output.WriteCommand(CommandExamples.ListCommandExample(args.CommandName));

            Reporter.Output.WriteLine(LocalizableStrings.TemplateInformationCoordinator_DotnetNew_SearchTemplatesHint);
            Reporter.Output.WriteCommand(CommandExamples.SearchCommandExample(args.CommandName, "web"));

            Reporter.Output.WriteLine();

            return NewCommandStatus.Success;
        }

        private static string GetInputParametersString(ListCommandArgs args/*, IReadOnlyDictionary<string, string?>? templateParameters = null*/)
        {
            string separator = ", ";
            IEnumerable<string> appliedFilters = args.AppliedFilters
                    .Select(filter => $"{args.GetFilterToken(filter)}='{args.GetFilterValue(filter)}'");

            //IEnumerable<string> appliedTemplateParameters = templateParameters?
            //       .Select(param => string.IsNullOrWhiteSpace(param.Value) ? param.Key : $"{param.Key}='{param.Value}'") ?? Array.Empty<string>();

            StringBuilder inputParameters = new StringBuilder();
            string? mainCriteria = args.ListNameCriteria;
            if (!string.IsNullOrWhiteSpace(mainCriteria))
            {
                inputParameters.Append($"'{mainCriteria}'");
                if (appliedFilters.Any()/* || appliedTemplateParameters.Any()*/)
                {
                    inputParameters.Append(separator);
                }
            }
            if (appliedFilters/*.Concat(appliedTemplateParameters)*/.Any())
            {
                inputParameters.Append(string.Join(separator, appliedFilters/*.Concat(appliedTemplateParameters)*/));
            }
            return inputParameters.ToString();
        }

        private static string GetPartialMatchReason(TemplateResolutionResult templateResolutionResult, ListCommandArgs args/*, IReadOnlyDictionary<string, string?>? templateParameters = null*/)
        {
            string separator = ", ";

            IEnumerable<string> appliedFilters = args.AppliedFilters
                    .OfType<TemplateFilterOptionDefinition>()
                    .Where(filter => filter.MismatchCriteria(templateResolutionResult))
                    .Select(filter => $"{args.GetFilterToken(filter)}='{args.GetFilterValue(filter)}'");

            //IEnumerable<string> appliedTemplateParameters = templateParameters?
            //       .Where(parameter =>
            //            templateResolutionResult.IsParameterMismatchReason(parameter.Key))
            //       .Select(param => string.IsNullOrWhiteSpace(param.Value) ? param.Key : $"{param.Key}='{param.Value}'") ?? Array.Empty<string>();

            StringBuilder inputParameters = new StringBuilder();
            if (appliedFilters/*.Concat(appliedTemplateParameters)*/.Any())
            {
                inputParameters.Append(string.Join(separator, appliedFilters/*.Concat(appliedTemplateParameters)*/));
            }
            return inputParameters.ToString();
        }

        /// <summary>
        /// Displays curated list of templates for dotnet new command.
        /// </summary>
        private async Task<IEnumerable<ITemplateInfo>> GetCuratedListAsync(CancellationToken cancellationToken)
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
    }
}
