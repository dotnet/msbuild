// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Cli.Extensions;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class TemplateCommand : Command, ICommandHandler
    {
        private static readonly string[] _helpAliases = new[] { "-h", "/h", "--help", "-?", "/?" };
        private readonly TemplatePackageManager _templatePackageManager;
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly InstantiateCommand _instantiateCommand;
        private readonly TemplateGroup _templateGroup;
        private readonly CliTemplateInfo _template;
        private Dictionary<string, TemplateOption> _templateSpecificOptions = new Dictionary<string, TemplateOption>();

        /// <summary>
        /// Create command for instantiation of specific template.
        /// </summary>
        /// <exception cref="InvalidTemplateParametersException">when <paramref name="template"/> has invalid template parameters.</exception>
        public TemplateCommand(
            InstantiateCommand instantiateCommand,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            TemplateGroup templateGroup,
            CliTemplateInfo template,
            bool buildDefaultLanguageValidation = false)
            : base(
                  templateGroup.ShortNames[0],
                  template.Name + Environment.NewLine + template.Description)
        {
            _instantiateCommand = instantiateCommand;
            _environmentSettings = environmentSettings;
            _templatePackageManager = templatePackageManager;
            _templateGroup = templateGroup;
            _template = template;
            foreach (var item in templateGroup.ShortNames.Skip(1))
            {
                AddAlias(item);
            }

            this.AddOption(OutputOption);
            this.AddOption(NameOption);
            this.AddOption(DryRunOption);
            this.AddOption(ForceOption);
            this.AddOption(NoUpdateCheckOption);

            string? templateLanguage = template.GetLanguage();
            string? defaultLanguage = environmentSettings.GetDefaultLanguage();
            if (!string.IsNullOrWhiteSpace(templateLanguage))
            {
                LanguageOption = SharedOptionsFactory.CreateLanguageOption();
                LanguageOption.Description = SymbolStrings.TemplateCommand_Option_Language;
                LanguageOption.FromAmong(templateLanguage);

                if (!string.IsNullOrWhiteSpace(defaultLanguage)
                     && buildDefaultLanguageValidation)
                {
                    LanguageOption.SetDefaultValue(defaultLanguage);
                    LanguageOption.AddValidator(optionResult =>
                    {
                        var value = optionResult.GetValueOrDefault<string>();
                        if (value != template.GetLanguage())
                        {
                            return "Languages don't match";
                        }
                        return null;
                    }
                    );
                }
                this.AddOption(LanguageOption);
            }

            string? templateType = template.GetTemplateType();

            if (!string.IsNullOrWhiteSpace(templateType))
            {
                TypeOption = SharedOptionsFactory.CreateTypeOption();
                TypeOption.Description = SymbolStrings.TemplateCommand_Option_Type;
                TypeOption.FromAmong(templateType);
                this.AddOption(TypeOption);
            }

            if (template.BaselineInfo.Any(b => !string.IsNullOrWhiteSpace(b.Key)))
            {
                BaselineOption = SharedOptionsFactory.CreateBaselineOption();
                BaselineOption.Description = SymbolStrings.TemplateCommand_Option_Baseline;
                BaselineOption.FromAmong(template.BaselineInfo.Select(b => b.Key).Where(b => !string.IsNullOrWhiteSpace(b)).ToArray());
                this.AddOption(BaselineOption);
            }

            if (HasRunScriptPostActionDefined(template))
            {
                AllowScriptsOption = new Option<AllowRunScripts>("--allow-scripts")
                {
                    Description = SymbolStrings.TemplateCommand_Option_AllowScripts,
                    Arity = new ArgumentArity(1, 1)
                };
                AllowScriptsOption.SetDefaultValue(AllowRunScripts.Prompt);
                this.AddOption(AllowScriptsOption);
            }

            AddTemplateOptionsToCommand(template);
            this.Handler = this;
        }

        internal static IReadOnlyList<string> KnownHelpAliases => _helpAliases;

        internal Option<string> OutputOption { get; } = SharedOptionsFactory.CreateOutputOption();

        internal Option<string> NameOption { get; } = new Option<string>(new string[] { "-n", "--name" })
        {
            Description = SymbolStrings.TemplateCommand_Option_Name,
            Arity = new ArgumentArity(1, 1)
        };

        internal Option<bool> DryRunOption { get; } = new Option<bool>("--dry-run")
        {
            Description = SymbolStrings.TemplateCommand_Option_DryRun,
            Arity = new ArgumentArity(0, 1)
        };

        internal Option<bool> ForceOption { get; } = new Option<bool>("--force")
        {
            Description = SymbolStrings.TemplateCommand_Option_Force,
            Arity = new ArgumentArity(0, 1)
        };

        internal Option<bool> NoUpdateCheckOption { get; } = new Option<bool>("--no-update-check")
        {
            Description = SymbolStrings.TemplateCommand_Option_NoUpdateCheck,
            Arity = new ArgumentArity(0, 1)
        };

        internal Option<AllowRunScripts>? AllowScriptsOption { get; }

        internal Option<string>? LanguageOption { get; }

        internal Option<string>? TypeOption { get; }

        internal Option<string>? BaselineOption { get; }

        internal IReadOnlyDictionary<string, TemplateOption> TemplateOptions => _templateSpecificOptions;

        internal CliTemplateInfo Template => _template;

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            TemplateCommandArgs args = new TemplateCommandArgs(this, context.ParseResult);

            TemplateInvoker invoker = new TemplateInvoker(_environmentSettings, _instantiateCommand.TelemetryLogger, () => Console.ReadLine() ?? string.Empty, _instantiateCommand.Callbacks);
            if (!args.NoUpdateCheck)
            {
                TemplatePackageCoordinator packageCoordinator = new TemplatePackageCoordinator(_instantiateCommand.TelemetryLogger, _environmentSettings, _templatePackageManager);
                Task<CheckUpdateResult?> checkForUpdateTask = packageCoordinator.CheckUpdateForTemplate(args.Template, context.GetCancellationToken());
                Task<NewCommandStatus> instantiateTask = invoker.InvokeTemplateAsync(args, context.GetCancellationToken());
                await Task.WhenAll(checkForUpdateTask, instantiateTask).ConfigureAwait(false);

                if (checkForUpdateTask?.Result != null)
                {
                    // print if there is update for this template
                    packageCoordinator.DisplayUpdateCheckResult(checkForUpdateTask.Result, args.NewCommandName);
                }
                // return creation result
                return (int)instantiateTask.Result;
            }
            else
            {
                return (int)await invoker.InvokeTemplateAsync(args, context.GetCancellationToken()).ConfigureAwait(false);
            }
        }

        private bool HasRunScriptPostActionDefined(CliTemplateInfo template)
        {
            return template.PostActions.Contains(ProcessStartPostActionProcessor.ActionProcessorId);
        }

        private HashSet<string> GetReservedAliases()
        {
            HashSet<string> reservedAliases = new HashSet<string>();
            foreach (string alias in this.Children.OfType<Option>().SelectMany(o => o.Aliases))
            {
                reservedAliases.Add(alias);
            }
            foreach (string alias in this.Children.OfType<Command>().SelectMany(o => o.Aliases))
            {
                reservedAliases.Add(alias);
            }
            //add options of parent? - this covers debug: options
            foreach (string alias in _instantiateCommand.Children.OfType<Option>().SelectMany(o => o.Aliases))
            {
                reservedAliases.Add(alias);
            }
            foreach (string alias in _instantiateCommand.Children.OfType<Command>().SelectMany(o => o.Aliases))
            {
                reservedAliases.Add(alias);
            }

            //add restricted aliases: language, type, baseline (they may be optional)
            foreach (string alias in new[] { SharedOptionsFactory.CreateLanguageOption(), SharedOptionsFactory.CreateTypeOption(), SharedOptionsFactory.CreateBaselineOption() }.SelectMany(o => o.Aliases))
            {
                reservedAliases.Add(alias);
            }

            foreach (string helpAlias in KnownHelpAliases)
            {
                reservedAliases.Add(helpAlias);
            }
            return reservedAliases;
        }

        private void AddTemplateOptionsToCommand(CliTemplateInfo templateInfo)
        {
            HashSet<string> initiallyTakenAliases = GetReservedAliases();

            var parametersWithAliasAssignments = AliasAssignmentCoordinator.AssignAliasesForParameter(templateInfo.CliParameters.Values, initiallyTakenAliases);
            if (parametersWithAliasAssignments.Any(p => p.Errors.Any()))
            {
                IReadOnlyDictionary<CliTemplateParameter, IReadOnlyList<string>> errors = parametersWithAliasAssignments
                    .Where(p => p.Errors.Any())
                    .ToDictionary(p => p.Parameter, p => p.Errors);
                throw new InvalidTemplateParametersException(templateInfo, errors);
            }

            foreach ((CliTemplateParameter parameter, IReadOnlyList<string> aliases, IReadOnlyList<string> _) in parametersWithAliasAssignments)
            {
                TemplateOption option = new TemplateOption(parameter, aliases);
                this.AddOption(option.Option);
                _templateSpecificOptions[parameter.Name] = option;
            }
        }
    }
}
