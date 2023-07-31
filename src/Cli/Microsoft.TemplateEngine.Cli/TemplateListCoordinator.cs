// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Cli.TabularOutput;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli
{
    internal class TemplateListCoordinator
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;
        private readonly TemplatePackageManager _templatePackageManager;
        private readonly IHostSpecificDataLoader _hostSpecificDataLoader;
        private readonly string? _defaultLanguage;
        private readonly TemplateConstraintManager _constraintManager;

        internal TemplateListCoordinator(
            IEngineEnvironmentSettings engineEnvironmentSettings,
            TemplatePackageManager templatePackageManager,
            IHostSpecificDataLoader hostSpecificDataLoader)

        {
            _engineEnvironmentSettings = engineEnvironmentSettings ?? throw new ArgumentNullException(nameof(engineEnvironmentSettings));
            _templatePackageManager = templatePackageManager ?? throw new ArgumentNullException(nameof(templatePackageManager));
            _hostSpecificDataLoader = hostSpecificDataLoader ?? throw new ArgumentNullException(nameof(hostSpecificDataLoader));
            _defaultLanguage = engineEnvironmentSettings.GetDefaultLanguage();
            _constraintManager = new TemplateConstraintManager(_engineEnvironmentSettings);
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

            ListTemplateResolver resolver = new ListTemplateResolver(_constraintManager, _templatePackageManager, _hostSpecificDataLoader);
            TemplateResolutionResult resolutionResult = await resolver.ResolveTemplatesAsync(args, _defaultLanguage, cancellationToken).ConfigureAwait(false);

            //IReadOnlyDictionary<string, string?>? appliedParameterMatches = resolutionResult.GetAllMatchedParametersList();
            if (resolutionResult.TemplateGroupsWithMatchingTemplateInfoAndParameters.Any())
            {
                Reporter.Output.WriteLine(LocalizableStrings.TemplatesFoundMatchingInputParameters, GetInputParametersString(args));
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
                    Reporter.Output.WriteLine(LocalizableStrings.Generic_CommandHints_Search);
                    Reporter.Output.WriteCommand(
                       Example
                           .For<NewCommand>(args.ParseResult)
                           .WithSubcommand<SearchCommand>()
                           .WithArgument(SearchCommand.NameArgument));
                    Reporter.Output.WriteLine();
                    return NewCommandStatus.Success;
                }

                // at least one criteria was specified.
                // No templates found matching the following input parameter(s): {0}.
                Reporter.Error.WriteLine(
                    string.Format(
                        LocalizableStrings.Generic_Info_NoMatchingTemplates,
                        GetInputParametersString(args/*, appliedParameterMatches*/))
                    .Bold().Red());

                if (resolutionResult.HasTemplateGroupMatches && resolutionResult.ListFilterMismatchGroupCount > 0)
                {
                    // {0} template(s) partially matched, but failed on {1}.
                    Reporter.Error.WriteLine(
                        string.Format(
                            LocalizableStrings.TemplatesNotValidGivenTheSpecifiedFilter,
                            resolutionResult.ListFilterMismatchGroupCount,
                            GetPartialMatchReason(resolutionResult, args/*, appliedParameterMatches*/))
                        .Bold().Red());
                }

                if (resolutionResult.HasTemplateGroupMatches && resolutionResult.ContraintsMismatchGroupCount > 0)
                {
                    // {0} template(s) are not displayed due to their constraints are not satisfied.
                    // To display them add "--ignore-constraints" option.
                    Reporter.Error.WriteLine(
                        string.Format(
                            LocalizableStrings.TemplateListCoordinator_Error_FailedConstraints,
                            resolutionResult.ContraintsMismatchGroupCount,
                            ListCommand.IgnoreConstraintsOption.Name)
                        .Bold().Red());
                }

                Reporter.Error.WriteLine();
                // To search for the templates on NuGet.org, run:
                Reporter.Error.WriteLine(LocalizableStrings.Generic_CommandHints_Search);
                if (string.IsNullOrWhiteSpace(args.ListNameCriteria))
                {
                    Reporter.Error.WriteCommand(
                             Example
                                 .For<NewCommand>(args.ParseResult)
                                 .WithSubcommand<SearchCommand>()
                                 .WithArgument(SearchCommand.NameArgument));
                }
                else
                {
                    Reporter.Error.WriteCommand(
                             Example
                                 .For<NewCommand>(args.ParseResult)
                                 .WithSubcommand<SearchCommand>()
                                 .WithArgument(SearchCommand.NameArgument, args.ListNameCriteria));
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

            Reporter.Output.WriteLine(LocalizableStrings.TemplateInformationCoordinator_DotnetNew_Description, Example.For<NewCommand>(args.ParseResult));

            Reporter.Output.WriteLine();

            Reporter.Output.WriteLine(LocalizableStrings.TemplateInformationCoordinator_DotnetNew_TemplatesHeader, Example.For<NewCommand>(args.ParseResult));
            TemplateGroupDisplay.DisplayTemplateList(
                _engineEnvironmentSettings,
                curatedTemplates,
                new TabularOutputSettings(_engineEnvironmentSettings.Environment),
                reporter: Reporter.Output);

            Reporter.Output.WriteLine(LocalizableStrings.TemplateInformationCoordinator_DotnetNew_ExampleHeader);
            Reporter.Output.WriteCommand(
               Example
                   .For<NewCommand>(args.ParseResult)
                   .WithArgument(NewCommand.ShortNameArgument, "console"));

            Reporter.Output.WriteLine();

            Reporter.Output.WriteLine(LocalizableStrings.TemplateInformationCoordinator_DotnetNew_DisplayOptionsHint);
            Reporter.Output.WriteCommand(
              Example
                  .For<NewCommand>(args.ParseResult)
                  .WithArgument(NewCommand.ShortNameArgument, "console")
                  .WithHelpOption());

            Reporter.Output.WriteLine(LocalizableStrings.TemplateInformationCoordinator_DotnetNew_ListTemplatesHint);

            Reporter.Output.WriteCommand(
               Example
                   .For<NewCommand>(args.ParseResult)
                   .WithSubcommand<ListCommand>());

            Reporter.Output.WriteLine(LocalizableStrings.TemplateInformationCoordinator_DotnetNew_SearchTemplatesHint);
            Reporter.Output.WriteCommand(
                     Example
                         .For<NewCommand>(args.ParseResult)
                         .WithSubcommand<SearchCommand>()
                         .WithArgument(SearchCommand.NameArgument, "web"));

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
