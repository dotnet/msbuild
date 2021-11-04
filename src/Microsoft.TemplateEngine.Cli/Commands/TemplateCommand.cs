// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Cli.Extensions;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class TemplateCommand : Command, ICommandHandler
    {
        private readonly TemplatePackageManager _templatePackageManager;
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly NewCommand _newCommand;
        private readonly TemplateGroup _templateGroup;
        private readonly CliTemplateInfo _template;
        private Dictionary<string, Option> _templateSpecificOptions = new Dictionary<string, Option>();

        public TemplateCommand(
            NewCommand newCommand,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            TemplateGroup templateGroup,
            CliTemplateInfo template)
            : base(
                  templateGroup.ShortNames[0],
                  template.Name + Environment.NewLine + template.Description)
        {
            _newCommand = newCommand;
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
            this.AddOption(AllowScriptsOption);

            string? templateLanguage = template.GetLanguage();

            if (!string.IsNullOrWhiteSpace(templateLanguage))
            {
                LanguageOption = SharedOptionsFactory.CreateLanguageOption();
                LanguageOption.FromAmong(templateLanguage);
                if (templateGroup.Languages.Count > 1)
                {
                    LanguageOption.SetDefaultValue(environmentSettings.GetDefaultLanguage());
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
                TypeOption.FromAmong(templateType);
                this.AddOption(TypeOption);
            }

            if (template.BaselineInfo.Any(b => string.IsNullOrWhiteSpace(b.Key)))
            {
                BaselineOption = SharedOptionsFactory.CreateBaselineOption();
                BaselineOption.FromAmong(template.BaselineInfo.Select(b => b.Key).Where(b => !string.IsNullOrWhiteSpace(b)).ToArray());
                this.AddOption(BaselineOption);
            }

            AddTemplateOptionsToCommand(template);
            this.Handler = this;
        }

        internal Option<string> OutputOption { get; } = new Option<string>(new string[] { "-o", "--output" })
        {
            Description = LocalizableStrings.OptionDescriptionOutput,
            Arity = new ArgumentArity(0, 1)
        };

        internal Option<string> NameOption { get; } = new Option<string>(new string[] { "-n", "--name" })
        {
            Description = LocalizableStrings.OptionDescriptionName,
            Arity = new ArgumentArity(0, 1)
        };

        internal Option<bool> DryRunOption { get; } = new Option<bool>("--dry-run")
        {
            Description = LocalizableStrings.OptionDescriptionDryRun,
            Arity = new ArgumentArity(0, 1)
        };

        internal Option<bool> ForceOption { get; } = new Option<bool>("--force")
        {
            Description = LocalizableStrings.OptionDescriptionForce,
            Arity = new ArgumentArity(0, 1)
        };

        internal Option<bool> NoUpdateCheckOption { get; } = new Option<bool>("--no-update-check")
        {
            Description = LocalizableStrings.OptionDescriptionNoUpdateCheck,
            Arity = new ArgumentArity(0, 1)
        };

        internal Option<AllowRunScripts> AllowScriptsOption { get; } = new Option<AllowRunScripts>("--allow-scripts")
        {
            Description = LocalizableStrings.OptionDescriptionAllowScripts,
            IsHidden = true,
            Arity = new ArgumentArity(0, 1)
        };

        internal Option<string>? LanguageOption { get; }

        internal Option<string>? TypeOption { get; }

        internal Option<string>? BaselineOption { get; }

        internal IReadOnlyDictionary<string, Option> TemplateOptions => _templateSpecificOptions;

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            TemplateArgs args = new TemplateArgs(this, _template, context.ParseResult);

            TemplateInvoker invoker = new TemplateInvoker(_environmentSettings, _newCommand.TelemetryLogger, () => Console.ReadLine() ?? string.Empty, _newCommand.Callbacks);
            if (!args.NoUpdateCheck)
            {
                TemplatePackageCoordinator packageCoordinator = new TemplatePackageCoordinator(_newCommand.TelemetryLogger, _environmentSettings, _templatePackageManager);
                Task<CheckUpdateResult?> checkForUpdateTask = packageCoordinator.CheckUpdateForTemplate(args.Template, context.GetCancellationToken());
                Task<NewCommandStatus> instantiateTask = invoker.InvokeTemplateAsync(args, context.GetCancellationToken());
                await Task.WhenAll(checkForUpdateTask, instantiateTask).ConfigureAwait(false);

                if (checkForUpdateTask?.Result != null)
                {
                    // print if there is update for this template
                    packageCoordinator.DisplayUpdateCheckResult(checkForUpdateTask.Result, _newCommand.Name);
                }
                // return creation result
                return (int)instantiateTask.Result;
            }
            else
            {
                return (int)await invoker.InvokeTemplateAsync(args, context.GetCancellationToken()).ConfigureAwait(false);
            }
        }

        private static ArgumentArity GetOptionArity(CliTemplateParameter parameter) => new ArgumentArity(parameter.IsRequired ? 1 : 0, 1);

        private HashSet<string> GetReservedAliases()
        {
            HashSet<string> reservedAliases = new HashSet<string>();
            foreach (string alias in this.Children.OfType<Option>().SelectMany(o => o.Aliases))
            {
                reservedAliases.Add(alias);
            }
            //add options of parent? - this covers debug: options
            foreach (string alias in _newCommand.SelectMany(p => p.Children).OfType<Option>().SelectMany(o => o.Aliases))
            {
                reservedAliases.Add(alias);
            }

            //add restricted aliases: language, type, baseline (they may be optional)
            foreach (string alias in new[] { SharedOptionsFactory.CreateLanguageOption(), SharedOptionsFactory.CreateTypeOption(), SharedOptionsFactory.CreateBaselineOption() }.SelectMany(o => o.Aliases))
            {
                reservedAliases.Add(alias);
            }
            return reservedAliases;
        }

        private void AddTemplateOptionsToCommand(CliTemplateInfo templateInfo)
        {
            IList<Option> paramOptionList = new List<Option>();
            HashSet<string> initiallyTakenAliases = GetReservedAliases();
            IEnumerable<CliTemplateParameter> parameters = templateInfo.GetParameters();

            //TODO: handle errors
            var parametersWithAliasAssignments = AliasAssignmentCoordinator.AssignAliasesForParameter(parameters, initiallyTakenAliases);

            foreach ((CliTemplateParameter parameter, IEnumerable<string> aliases, IEnumerable<string> errors) in parametersWithAliasAssignments)
            {
                Option option = parameter.Type switch
                {
                    ParameterType.Boolean => new Option<bool>(aliases.ToArray())
                    {
                        Arity = GetOptionArity(parameter),
                    },
                    ParameterType.Integer => new Option<int>(aliases.ToArray())
                    {
                        Arity = GetOptionArity(parameter),
                    },
                    ParameterType.String => new Option<string>(aliases.ToArray())
                    {
                        Arity = GetOptionArity(parameter),
                    },
                    ParameterType.Choice => CreateChoiceOption(parameter, aliases),
                    ParameterType.Float => new Option<float>(aliases.ToArray())
                    {
                        Arity = GetOptionArity(parameter),
                    },
                    ParameterType.Hex => CreateHexOption(parameter, aliases),
                    _ => throw new Exception($"Unexpected value for {nameof(ParameterType)}: {parameter.Type}.")
                };
                option.IsHidden = parameter.IsHidden;
                option.IsRequired = parameter.IsRequired;
                option.SetDefaultValue(parameter.DefaultValue);
                option.Description = parameter.Description;

                this.AddOption(option);
                _templateSpecificOptions[parameter.Name] = option;
            }
        }

        //TODO
        private Option CreateHexOption(CliTemplateParameter parameter, IEnumerable<string> aliases) => throw new NotImplementedException();

        private Option CreateChoiceOption(CliTemplateParameter parameter, IEnumerable<string> aliases)
        {
            Option option = new Option<string>(aliases.ToArray())
            {
                Arity = GetOptionArity(parameter),
            };
            if (parameter.Choices != null)
            {
                option.FromAmong(parameter.Choices.Keys.ToArray());
            }
            return option;
        }

    }
}
