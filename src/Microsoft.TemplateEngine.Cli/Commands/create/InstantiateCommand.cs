// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Cli.Extensions;
using Microsoft.TemplateEngine.Cli.TabularOutput;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal partial class InstantiateCommand : BaseCommand<InstantiateCommandArgs>, ICustomHelp
    {
        private NewCommand? _parentCommand;

        internal InstantiateCommand(
            Func<ParseResult, ITemplateEngineHost> hostBuilder,
            Func<ParseResult, ITelemetryLogger> telemetryLoggerBuilder,
            NewCommandCallbacks callbacks)
            : base(hostBuilder, telemetryLoggerBuilder, callbacks, "create", SymbolStrings.Command_Instantiate_Description)
        {
            this.AddArgument(ShortNameArgument);
            this.AddArgument(RemainingArguments);
            IsHidden = true;
        }

        private InstantiateCommand(NewCommand parentCommand, string name, string description)
            : base(parentCommand, name, description)
        {
            _parentCommand = parentCommand;
            this.AddArgument(ShortNameArgument);
            this.AddArgument(RemainingArguments);
        }

        internal Argument<string> ShortNameArgument { get; } = new Argument<string>("template-short-name")
        {
            Description = SymbolStrings.Command_Instantiate_Argument_ShortName,
            Arity = new ArgumentArity(0, 1)
        };

        internal Argument<string[]> RemainingArguments { get; } = new Argument<string[]>("template-args")
        {
            Description = SymbolStrings.Command_Instantiate_Argument_TemplateOptions,
            Arity = new ArgumentArity(0, 999)
        };

        internal static InstantiateCommand FromNewCommand(NewCommand parentCommand)
        {
            InstantiateCommand command = new InstantiateCommand(
                parentCommand,
                parentCommand.Name,
                parentCommand.Description ?? SymbolStrings.Command_New_Description);
            //subcommands are re-added just for the sake of proper help display
            foreach (var subcommand in parentCommand.Children.OfType<Command>())
            {
                command.Add(subcommand);
            }

            //do not re-add legacy options - they are not needed
            //do not hardcode option as the caller of CLI (SDK) can add extra options that should be moved
            foreach (var option in parentCommand.Children.OfType<Option>().Where(o => !parentCommand.LegacyOptions.Contains(o)))
            {
                command.Add(option);
            }
            return command;
        }

        internal Task<NewCommandStatus> ExecuteAsync(ParseResult parseResult, IEngineEnvironmentSettings environmentSettings, ITelemetryLogger telemetryLogger, InvocationContext context)
        {
            return ExecuteAsync(ParseContext(parseResult), environmentSettings, telemetryLogger, context);
        }

        internal HashSet<TemplateCommand> GetTemplateCommand(
                InstantiateCommandArgs args,
                IEngineEnvironmentSettings environmentSettings,
                TemplatePackageManager templatePackageManager,
                TemplateGroup templateGroup)
        {
            //groups templates in the group by precedence
            foreach (IGrouping<int, CliTemplateInfo> templateGrouping in templateGroup.Templates.GroupBy(g => g.Precedence).OrderByDescending(g => g.Key))
            {
                HashSet<TemplateCommand> candidates = ReparseForTemplate(
                    args,
                    environmentSettings,
                    templatePackageManager,
                    templateGroup,
                    templateGrouping,
                    out bool languageOptionSpecified);

                //if no candidates continue with next precedence
                if (!candidates.Any())
                {
                    continue;
                }
                //if language option is not specified, we do not need to do reparsing for default language
                if (languageOptionSpecified || string.IsNullOrWhiteSpace(environmentSettings.GetDefaultLanguage()))
                {
                    return candidates;
                }

                // try to reparse for default language
                return ReparseForDefaultLanguage(
                    args,
                    environmentSettings,
                    templatePackageManager,
                    templateGroup,
                    candidates);
            }
            return new HashSet<TemplateCommand>();
        }

        internal IEngineEnvironmentSettings GetEnvironmentSettingsFromArgs(InstantiateCommandArgs instantiateArgs)
        {
            return this.CreateEnvironmentSettings(instantiateArgs, instantiateArgs.ParseResult);
        }

        internal async Task<IEnumerable<TemplateGroup>> GetMatchingTemplateGroupsAsync(
            InstantiateCommandArgs instantiateArgs,
            TemplatePackageManager templatePackageManager,
            HostSpecificDataLoader hostSpecificDataLoader,
            CancellationToken cancellationToken)
        {
            var templates = await templatePackageManager.GetTemplatesAsync(cancellationToken).ConfigureAwait(false);
            var templateGroups = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templates, hostSpecificDataLoader));
            return templateGroups.Where(template => template.ShortNames.Contains(instantiateArgs.ShortName));
        }

        internal void HandleNoMatchingTemplateGroup(InstantiateCommandArgs instantiateArgs, Reporter reporter)
        {
            reporter.WriteLine(
                string.Format(LocalizableStrings.NoTemplatesMatchingInputParameters, $"'{instantiateArgs.ShortName}'").Bold().Red());
            reporter.WriteLine();

            reporter.WriteLine(LocalizableStrings.ListTemplatesCommand);
            reporter.WriteCommand(CommandExamples.ListCommandExample(instantiateArgs.CommandName));

            reporter.WriteLine(LocalizableStrings.SearchTemplatesCommand);
            reporter.WriteCommand(CommandExamples.SearchCommandExample(instantiateArgs.CommandName, instantiateArgs.ShortName));
            reporter.WriteLine();
        }

        internal NewCommandStatus HandleAmbiguousTemplateGroup(
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            IEnumerable<TemplateGroup> templateGroups,
            Reporter reporter,
            CancellationToken cancellationToken = default)
        {
            IEnvironment environment = environmentSettings.Environment;
            reporter.WriteLine(LocalizableStrings.AmbiguousTemplatesHeader.Bold().Red());
            TabularOutput<TemplateGroup> formatter =
                TabularOutput.TabularOutput
                    .For(
                        new TabularOutputSettings(environment),
                        templateGroups)
                    .DefineColumn(t => t.GroupIdentity ?? t.Templates[0].Identity, out object identityColumn, LocalizableStrings.ColumnNameIdentity, showAlways: true)
                    .DefineColumn(t => t.Name, LocalizableStrings.ColumnNameTemplateName, shrinkIfNeeded: true, minWidth: 15, showAlways: true)
                    .DefineColumn(t => string.Join(",", t.ShortNames), LocalizableStrings.ColumnNameShortName, showAlways: true)
                    .DefineColumn(t => string.Join(",", t.Languages), LocalizableStrings.ColumnNameLanguage, showAlways: true)
                    .DefineColumn(t => string.Join(",", t.Authors), LocalizableStrings.ColumnNameAuthor, showAlways: true, shrinkIfNeeded: true, minWidth: 10)
                    .DefineColumn(t => Task.Run(() => GetTemplatePackagesList(t)).GetAwaiter().GetResult(), LocalizableStrings.ColumnNamePackage, showAlways: true)
                    .OrderBy(identityColumn, StringComparer.CurrentCultureIgnoreCase);

            reporter.WriteLine(formatter.Layout().Bold().Red());
            reporter.WriteLine(LocalizableStrings.AmbiguousTemplatesMultiplePackagesHint.Bold().Red());
            return NewCommandStatus.NotFound;

            async Task<string> GetTemplatePackagesList(TemplateGroup templateGroup)
            {
                try
                {
                    IReadOnlyList<IManagedTemplatePackage> templatePackages =
                        await templateGroup.GetManagedTemplatePackagesAsync(templatePackageManager, cancellationToken).ConfigureAwait(false);
                    return string.Join(environment.NewLine, templatePackages.Select(templatePackage => templatePackage.Identifier));
                }
                catch (Exception ex)
                {
                    environmentSettings.Host.Logger.LogWarning($"Failed to get information about template packages for template group {templateGroup.GroupIdentity}.");
                    environmentSettings.Host.Logger.LogDebug($"Details: {ex}.");
                    return string.Empty;
                }
            }
        }

        protected async override Task<NewCommandStatus> ExecuteAsync(
            InstantiateCommandArgs instantiateArgs,
            IEngineEnvironmentSettings environmentSettings,
            ITelemetryLogger telemetryLogger,
            InvocationContext context)
        {
            var cancellationToken = context.GetCancellationToken();
            using TemplatePackageManager templatePackageManager = new TemplatePackageManager(environmentSettings);
            HostSpecificDataLoader hostSpecificDataLoader = new HostSpecificDataLoader(environmentSettings);
            if (string.IsNullOrWhiteSpace(instantiateArgs.ShortName))
            {
                TemplateListCoordinator templateListCoordinator = new TemplateListCoordinator(
                    environmentSettings,
                    templatePackageManager,
                    hostSpecificDataLoader,
                    telemetryLogger);

                return await templateListCoordinator.DisplayCommandDescriptionAsync(instantiateArgs, cancellationToken).ConfigureAwait(false);
            }

            var selectedTemplateGroups = await GetMatchingTemplateGroupsAsync(
                instantiateArgs,
                templatePackageManager,
                hostSpecificDataLoader,
                cancellationToken).ConfigureAwait(false);

            if (!selectedTemplateGroups.Any())
            {
                HandleNoMatchingTemplateGroup(instantiateArgs, Reporter.Error);
                return NewCommandStatus.NotFound;
            }
            if (selectedTemplateGroups.Count() > 1)
            {
                return HandleAmbiguousTemplateGroup(environmentSettings, templatePackageManager, selectedTemplateGroups, Reporter.Error, cancellationToken);
            }
            return await HandleTemplateInstantationAsync(
                instantiateArgs,
                environmentSettings,
                telemetryLogger,
                templatePackageManager,
                selectedTemplateGroups.Single(),
                cancellationToken).ConfigureAwait(false);
        }

        protected override InstantiateCommandArgs ParseContext(ParseResult parseResult) => new(this, parseResult);

        private static NewCommandStatus HandleAmbiguousLanguage(
            IEngineEnvironmentSettings environmentSettings,
            IEnumerable<CliTemplateInfo> templates,
            Reporter reporter)
        {
            reporter.WriteLine(HelpStrings.TableHeader_AmbiguousTemplatesList);
            TemplateGroupDisplay.DisplayTemplateList(
                environmentSettings,
                templates,
                new TabularOutputSettings(environmentSettings.Environment),
                reporter);
            reporter.WriteLine(HelpStrings.Hint_AmbiguousLanguage);
            return NewCommandStatus.NotFound;
        }

        private static NewCommandStatus HandleAmbiguousType(
            IEngineEnvironmentSettings environmentSettings,
            IEnumerable<CliTemplateInfo> templates,
            Reporter reporter)
        {
            reporter.WriteLine(HelpStrings.TableHeader_AmbiguousTemplatesList);
            TemplateGroupDisplay.DisplayTemplateList(
                environmentSettings,
                templates,
                new TabularOutputSettings(
                    environmentSettings.Environment,
                    columnsToDisplay: new[] { TabularOutputSettings.ColumnNames.Type }),
                reporter);
            reporter.WriteLine(HelpStrings.Hint_AmbiguousType);
            return NewCommandStatus.NotFound;
        }

        private async Task<NewCommandStatus> HandleTemplateInstantationAsync(
            InstantiateCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            ITelemetryLogger telemetryLogger,
            TemplatePackageManager templatePackageManager,
            TemplateGroup templateGroup,
            CancellationToken cancellationToken)
        {
            HashSet<TemplateCommand> candidates = GetTemplateCommand(args, environmentSettings, templatePackageManager, templateGroup);
            if (candidates.Count == 1)
            {
                Command commandToRun = _parentCommand is null ? this : _parentCommand;
                TemplateCommand templateCommandToRun = candidates.Single();
                templateCommandToRun.SetHandler((InvocationContext context) => templateCommandToRun.InvokeAsync(context, telemetryLogger));
                commandToRun.AddCommand(templateCommandToRun);
                return (NewCommandStatus)await commandToRun.InvokeAsync(args.TokensToInvoke).ConfigureAwait(false);
            }
            else if (candidates.Any())
            {
                return HandleAmbiguousResult(
                    environmentSettings,
                    templatePackageManager,
                    candidates.Select(c => c.Template),
                    Reporter.Error,
                    cancellationToken);
            }

            return HandleNoTemplateFoundResult(args, environmentSettings, templatePackageManager, templateGroup, Reporter.Error);
        }

        private NewCommandStatus HandleAmbiguousResult(
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            IEnumerable<CliTemplateInfo> templates,
            Reporter reporter,
            CancellationToken cancellationToken = default)
        {
            if (!templates.Any(t => string.IsNullOrWhiteSpace(t.GetLanguage()))
                && !templates.AllAreTheSame(t => t.GetLanguage()))
            {
                return HandleAmbiguousLanguage(
                       environmentSettings,
                       templates,
                       Reporter.Error);
            }

            if (!templates.Any(t => string.IsNullOrWhiteSpace(t.GetTemplateType()))
            && !templates.AllAreTheSame(t => t.GetTemplateType()))
            {
                return HandleAmbiguousType(
                       environmentSettings,
                       templates,
                       Reporter.Error);
            }

            reporter.WriteLine(LocalizableStrings.AmbiguousTemplatesHeader.Bold().Red());
            IEnvironment environment = environmentSettings.Environment;
            TabularOutput<CliTemplateInfo> formatter =
                    TabularOutput.TabularOutput
                        .For(
                            new TabularOutputSettings(environment),
                            templates)
                        .DefineColumn(t => t.Identity, out object identityColumn, LocalizableStrings.ColumnNameIdentity, showAlways: true)
                        .DefineColumn(t => t.Name, LocalizableStrings.ColumnNameTemplateName, shrinkIfNeeded: true, minWidth: 15, showAlways: true)
                        .DefineColumn(t => string.Join(",", t.ShortNameList), LocalizableStrings.ColumnNameShortName, showAlways: true)
                        .DefineColumn(t => t.GetLanguage(), LocalizableStrings.ColumnNameLanguage, showAlways: true)
                        .DefineColumn(t => t.Precedence.ToString(), out object prcedenceColumn, LocalizableStrings.ColumnNamePrecedence, showAlways: true)
                        .DefineColumn(t => t.Author, LocalizableStrings.ColumnNameAuthor, showAlways: true, shrinkIfNeeded: true, minWidth: 10)
                        .DefineColumn(t => Task.Run(() => GetTemplatePackage(t)).GetAwaiter().GetResult(), LocalizableStrings.ColumnNamePackage, showAlways: true)
                        .OrderBy(identityColumn, StringComparer.CurrentCultureIgnoreCase)
                        .OrderByDescending(prcedenceColumn, new NullOrEmptyIsLastStringComparer());
            reporter.WriteLine(formatter.Layout().Bold().Red());

            reporter.WriteLine(LocalizableStrings.AmbiguousTemplatesMultiplePackagesHint.Bold().Red());
            if (templates.AllAreTheSame(t => t.MountPointUri))
            {
                string templatePackage = Task.Run(() => GetTemplatePackage(templates.First())).GetAwaiter().GetResult();
                if (!string.IsNullOrWhiteSpace(templatePackage))
                {
                    reporter.WriteLine(string.Format(LocalizableStrings.AmbiguousTemplatesSamePackageHint, templatePackage).Bold().Red());
                }
            }
            return NewCommandStatus.NotFound;

            async Task<string> GetTemplatePackage(CliTemplateInfo template)
            {
                try
                {
                    IManagedTemplatePackage? templatePackage =
                        await template.GetManagedTemplatePackageAsync(templatePackageManager, cancellationToken).ConfigureAwait(false);
                    return templatePackage?.Identifier ?? string.Empty;
                }
                catch (Exception ex)
                {
                    environmentSettings.Host.Logger.LogWarning($"Failed to get information about template packages for template group {template.Identity}.");
                    environmentSettings.Host.Logger.LogDebug($"Details: {ex}.");
                    return string.Empty;
                }
            }
        }

        private HashSet<TemplateCommand> ReparseForTemplate(
            InstantiateCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            TemplateGroup templateGroup,
            IEnumerable<CliTemplateInfo> templatesToReparse,
            out bool languageOptionSpecified)
        {
            languageOptionSpecified = false;
            HashSet<TemplateCommand> candidates = new HashSet<TemplateCommand>();
            foreach (CliTemplateInfo template in templatesToReparse)
            {
                if (ReparseForTemplate(args, environmentSettings, templatePackageManager, templateGroup, template) is (TemplateCommand command, ParseResult parseResult))
                {
                    languageOptionSpecified = command.LanguageOption != null
                        && parseResult.FindResultFor(command.LanguageOption) != null;
                    if (!parseResult.Errors.Any())
                    {
                        candidates.Add(command);
                    }
                }
            }
            return candidates;
        }

        private HashSet<TemplateCommand> ReparseForDefaultLanguage(
            InstantiateCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            TemplateGroup templateGroup,
            HashSet<TemplateCommand> candidates)
        {
            HashSet<TemplateCommand> languageAwareCandidates = new HashSet<TemplateCommand>();
            foreach (var templateCommand in candidates)
            {
                if (ReparseForTemplate(
                    args,
                    environmentSettings,
                    templatePackageManager,
                    templateGroup,
                    templateCommand.Template,
                    validateDefaultLanguage: true) is (TemplateCommand command, ParseResult parseResult))
                {
                    if (!parseResult.Errors.Any())
                    {
                        languageAwareCandidates.Add(command);
                    }
                }
            }
            return languageAwareCandidates.Any()
                ? languageAwareCandidates
                : candidates;
        }

        private (TemplateCommand? Command, ParseResult? ParseResult)? ReparseForTemplate(
            InstantiateCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            TemplateGroup templateGroup,
            CliTemplateInfo template,
            bool validateDefaultLanguage = false)
        {
            try
            {
                TemplateCommand command = new TemplateCommand(
                    this,
                    environmentSettings,
                    templatePackageManager,
                    templateGroup,
                    template,
                    validateDefaultLanguage);

                Parser parser = ParserFactory.CreateParser(command);
                ParseResult parseResult = parser.Parse(args.RemainingArguments ?? Array.Empty<string>());
                return (command, parseResult);
            }
            catch (InvalidTemplateParametersException e)
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.GenericWarning, e.Message));
                return null;
            }
        }
    }
}
