// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class TemplateCommand : CliCommand
    {
        private static readonly TimeSpan ConstraintEvaluationTimeout = TimeSpan.FromMilliseconds(1000);
        private static readonly string[] _helpAliases = new[] { "-h", "/h", "--help", "-?", "/?" };
        private readonly TemplatePackageManager _templatePackageManager;
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly BaseCommand _instantiateCommand;
        private readonly TemplateGroup _templateGroup;
        private readonly CliTemplateInfo _template;
        private Dictionary<string, TemplateOption> _templateSpecificOptions = new();

        /// <summary>
        /// Create command for instantiation of specific template.
        /// </summary>
        /// <exception cref="InvalidTemplateParametersException">when <paramref name="template"/> has invalid template parameters.</exception>
        public TemplateCommand(
            BaseCommand instantiateCommand,
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
                Aliases.Add(item);
            }

            Options.Add(SharedOptions.OutputOption);
            Options.Add(SharedOptions.NameOption);
            Options.Add(SharedOptions.DryRunOption);
            Options.Add(SharedOptions.ForceOption);
            Options.Add(SharedOptions.NoUpdateCheckOption);

            string? templateLanguage = template.GetLanguage();
            string? defaultLanguage = environmentSettings.GetDefaultLanguage();
            if (!string.IsNullOrWhiteSpace(templateLanguage))
            {
                LanguageOption = SharedOptionsFactory.CreateLanguageOption();
                LanguageOption.Description = SymbolStrings.TemplateCommand_Option_Language;
                LanguageOption.FromAmongCaseInsensitive(new[] { templateLanguage });

                if (!string.IsNullOrWhiteSpace(defaultLanguage)
                     && buildDefaultLanguageValidation)
                {
                    LanguageOption.DefaultValueFactory = (_) => defaultLanguage;
                    LanguageOption.Validators.Add(optionResult =>
                    {
                        var value = optionResult.GetValueOrDefault<string>();
                        if (value != template.GetLanguage())
                        {
                            optionResult.AddError("Languages don't match");
                        }
                    }
                    );
                }
                Options.Add(LanguageOption);
            }

            string? templateType = template.GetTemplateType();

            if (!string.IsNullOrWhiteSpace(templateType))
            {
                TypeOption = SharedOptionsFactory.CreateTypeOption();
                TypeOption.Description = SymbolStrings.TemplateCommand_Option_Type;
                TypeOption.FromAmongCaseInsensitive(new[] { templateType });
                Options.Add(TypeOption);
            }

            if (template.BaselineInfo.Any(b => !string.IsNullOrWhiteSpace(b.Key)))
            {
                BaselineOption = SharedOptionsFactory.CreateBaselineOption();
                BaselineOption.Description = SymbolStrings.TemplateCommand_Option_Baseline;
                BaselineOption.FromAmongCaseInsensitive(template.BaselineInfo.Select(b => b.Key).Where(b => !string.IsNullOrWhiteSpace(b)).ToArray());
                Options.Add(BaselineOption);
            }

            if (HasRunScriptPostActionDefined(template))
            {
                AllowScriptsOption = new CliOption<AllowRunScripts>("--allow-scripts")
                {
                    Description = SymbolStrings.TemplateCommand_Option_AllowScripts,
                    Arity = new ArgumentArity(1, 1),
                    DefaultValueFactory = (_) => AllowRunScripts.Prompt
                };
                Options.Add(AllowScriptsOption);
            }

            AddTemplateOptionsToCommand(template);
        }

        internal static IReadOnlyList<string> KnownHelpAliases => _helpAliases;

        internal CliOption<AllowRunScripts>? AllowScriptsOption { get; }

        internal CliOption<string>? LanguageOption { get; }

        internal CliOption<string>? TypeOption { get; }

        internal CliOption<string>? BaselineOption { get; }

        internal IReadOnlyDictionary<string, TemplateOption> TemplateOptions => _templateSpecificOptions;

        internal CliTemplateInfo Template => _template;

        internal static async Task<IReadOnlyList<TemplateConstraintResult>> ValidateConstraintsAsync(TemplateConstraintManager constraintManager, ITemplateInfo template, CancellationToken cancellationToken)
        {
            if (!template.Constraints.Any())
            {
                return Array.Empty<TemplateConstraintResult>();
            }

            IReadOnlyList<(ITemplateInfo Template, IReadOnlyList<TemplateConstraintResult> Result)> result = await constraintManager.EvaluateConstraintsAsync(new[] { template }, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<TemplateConstraintResult> templateConstraints = result.Single().Result;

            if (templateConstraints.IsTemplateAllowed())
            {
                return Array.Empty<TemplateConstraintResult>();
            }
            return templateConstraints.Where(cr => cr.EvaluationStatus != TemplateConstraintResult.Status.Allowed).ToList();
        }

        internal async Task<NewCommandStatus> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken)
        {
            TemplateCommandArgs args = new(this, _instantiateCommand, parseResult);
            TemplateInvoker invoker = new(_environmentSettings, () => Console.ReadLine() ?? string.Empty);
            TemplatePackageCoordinator packageCoordinator = new(_environmentSettings, _templatePackageManager);
            TemplateConstraintManager constraintManager = new(_environmentSettings);
            TemplatePackageDisplay templatePackageDisplay = new(Reporter.Output, Reporter.Error);

            CancellationTokenSource cancellationTokenSource = new();
            cancellationTokenSource.CancelAfter(ConstraintEvaluationTimeout);

            Task<IReadOnlyList<TemplateConstraintResult>> constraintsEvaluation = ValidateConstraintsAsync(constraintManager, args.Template, args.IsForceFlagSpecified ? cancellationTokenSource.Token : cancellationToken);

            if (!args.IsForceFlagSpecified)
            {
                var constraintResults = await constraintsEvaluation.ConfigureAwait(false);
                if (constraintResults.Any())
                {
                    DisplayConstraintResults(constraintResults, args);
                    return NewCommandStatus.CreateFailed;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            Task<NewCommandStatus> instantiateTask = invoker.InvokeTemplateAsync(args, cancellationToken);
            Task<(string Id, string Version, string Provider)> builtInPackageCheck = packageCoordinator.ValidateBuiltInPackageAvailabilityAsync(args.Template, cancellationToken);
            Task<CheckUpdateResult?> checkForUpdateTask = packageCoordinator.CheckUpdateForTemplate(args, cancellationToken);

            Task[] tasksToWait = new Task[] { instantiateTask, builtInPackageCheck, checkForUpdateTask };

            await Task.WhenAll(tasksToWait).ConfigureAwait(false);
            Reporter.Output.WriteLine();

            cancellationToken.ThrowIfCancellationRequested();

            if (checkForUpdateTask.Result != null)
            {
                // print if there is update for the template package containing the template
                templatePackageDisplay.DisplayUpdateCheckResult(checkForUpdateTask.Result, args);
            }

            if (builtInPackageCheck.Result != default)
            {
                // print if there is same or newer built-in package
                templatePackageDisplay.DisplayBuiltInPackagesCheckResult(
                    builtInPackageCheck.Result.Id,
                    builtInPackageCheck.Result.Version,
                    builtInPackageCheck.Result.Provider,
                    args);
            }

            if (args.IsForceFlagSpecified)
            {
                // print warning about the constraints that were not met.
                try
                {
                    IReadOnlyList<TemplateConstraintResult> constraintResults = await constraintsEvaluation.WaitAsync(cancellationTokenSource.Token).ConfigureAwait(false);
                    if (constraintResults.Any())
                    {
                        DisplayConstraintResults(constraintResults, args);
                    }
                }
                catch (TaskCanceledException)
                {
                    // do nothing
                }
            }

            return instantiateTask.Result;
        }

        private void DisplayConstraintResults(IReadOnlyList<TemplateConstraintResult> constraintResults, TemplateCommandArgs templateArgs)
        {
            var reporter = templateArgs.IsForceFlagSpecified ? Reporter.Output : Reporter.Error;

            if (templateArgs.IsForceFlagSpecified)
            {
                reporter.WriteLine(LocalizableStrings.TemplateCommand_DisplayConstraintResults_Warning, templateArgs.Template.Name);
            }
            else
            {
                reporter.WriteLine(LocalizableStrings.TemplateCommand_DisplayConstraintResults_Error, templateArgs.Template.Name);
            }

            foreach (var constraint in constraintResults.Where(cr => cr.EvaluationStatus != TemplateConstraintResult.Status.Allowed))
            {
                reporter.WriteLine(constraint.ToDisplayString().Indent());
            }
            reporter.WriteLine();

            if (!templateArgs.IsForceFlagSpecified)
            {
                reporter.WriteLine(LocalizableStrings.TemplateCommand_DisplayConstraintResults_Hint, SharedOptions.ForceOption.Name);
                reporter.WriteCommand(Example.FromExistingTokens(templateArgs.ParseResult).WithOption(SharedOptions.ForceOption));
            }
            else
            {
                reporter.WriteLine(LocalizableStrings.TemplateCommand_DisplayConstraintResults_Hint_TemplateNotUsable);
            }
        }

        private bool HasRunScriptPostActionDefined(CliTemplateInfo template)
        {
            return template.PostActions.Contains(ProcessStartPostActionProcessor.ActionProcessorId);
        }

        private HashSet<string> GetReservedAliases()
        {
            HashSet<string> reservedAliases = new();
            AddReservedNamesAndAliases(reservedAliases, this);
            //add options of parent? - this covers debug: options
            AddReservedNamesAndAliases(reservedAliases, _instantiateCommand);

            //add restricted aliases: language, type, baseline (they may be optional)
            foreach (var option in new[] { SharedOptionsFactory.CreateLanguageOption(), SharedOptionsFactory.CreateTypeOption(), SharedOptionsFactory.CreateBaselineOption() })
            {
                reservedAliases.Add(option.Name);

                foreach (string alias in option.Aliases)
                {
                    reservedAliases.Add(alias);
                }
            }

            foreach (string helpAlias in KnownHelpAliases)
            {
                reservedAliases.Add(helpAlias);
            }
            return reservedAliases;

            static void AddReservedNamesAndAliases(HashSet<string> reservedAliases, CliCommand command)
            {
                foreach (CliOption option in command.Options)
                {
                    reservedAliases.Add(option.Name);
                    foreach (string alias in option.Aliases)
                    {
                        reservedAliases.Add(alias);
                    }
                }
                foreach (CliCommand subCommand in command.Subcommands)
                {
                    reservedAliases.Add(subCommand.Name);
                    foreach (string alias in subCommand.Aliases)
                    {
                        reservedAliases.Add(alias);
                    }
                }
            }
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

            foreach ((CliTemplateParameter parameter, IReadOnlySet<string> aliases, IReadOnlyList<string> _) in parametersWithAliasAssignments)
            {
                TemplateOption option = new(parameter, aliases);
                Options.Add(option.Option);
                _templateSpecificOptions[parameter.Name] = option;
            }
        }
    }
}
