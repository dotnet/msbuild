// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Globalization;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Cli.Extensions;
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
        private Dictionary<string, Option> _templateSpecificOptions = new Dictionary<string, Option>();

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
            this.AddOption(AllowScriptsOption);

            string? templateLanguage = template.GetLanguage();
            string? defaultLanguage = environmentSettings.GetDefaultLanguage();
            if (!string.IsNullOrWhiteSpace(templateLanguage))
            {
                LanguageOption = SharedOptionsFactory.CreateLanguageOption();
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

        internal static IReadOnlyList<string> KnownHelpAliases => _helpAliases;

        internal Option<string> OutputOption { get; } = SharedOptionsFactory.CreateOutputOption();

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

        internal CliTemplateInfo Template => _template;

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            TemplateArgs args = new TemplateArgs(this, context.ParseResult);

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
                        Arity = new ArgumentArity(0, 1)
                    },
                    ParameterType.Integer => new Option<long>(
                        aliases.ToArray(),
                        parseArgument: result => GetParseArgument(parameter, ConvertValueToInt)(result))
                    {
                        Arity = new ArgumentArity(0, 1)
                    },
                    ParameterType.String => new Option<string>(
                        aliases.ToArray(),
                        parseArgument: result => GetParseArgument(parameter, ConvertValueToString)(result))
                    {
                        Arity = new ArgumentArity(0, 1)
                    },
                    ParameterType.Choice => CreateChoiceOption(parameter, aliases),
                    ParameterType.Float => new Option<double>(
                        aliases.ToArray(),
                        parseArgument: result => GetParseArgument(parameter, ConvertValueToFloat)(result))
                    {
                        Arity = new ArgumentArity(0, 1)
                    },
                    ParameterType.Hex => new Option<long>(
                       aliases.ToArray(),
                       parseArgument: result => GetParseArgument(parameter, ConvertValueToHex)(result))
                    {
                        Arity = new ArgumentArity(0, 1)
                    },
                    _ => throw new Exception($"Unexpected value for {nameof(ParameterType)}: {parameter.Type}.")
                };
                option.IsHidden = parameter.IsHidden;
                option.IsRequired = parameter.IsRequired;
                if (!string.IsNullOrWhiteSpace(parameter.DefaultValue))
                {
                    option.SetDefaultValue(parameter.DefaultValue);
                }
                if (parameter.Type == ParameterType.String && parameter.DefaultValue != null)
                {
                    option.SetDefaultValue(parameter.DefaultValue);
                }
                option.Description = parameter.Description;

                this.AddOption(option);
                _templateSpecificOptions[parameter.Name] = option;
            }
        }

        private Option CreateChoiceOption(CliTemplateParameter parameter, IEnumerable<string> aliases)
        {
            Option option = new Option<string>(
                aliases.ToArray(),
                parseArgument: result => GetParseChoiceArgument(parameter)(result))
            {
                Arity = new ArgumentArity(0, 1)
            };
            if (parameter.Choices != null)
            {
                option.FromAmong(parameter.Choices.Keys.ToArray());
            }
            return option;
        }

        private ParseArgument<T> GetParseArgument<T>(CliTemplateParameter parameter, Func<string?, (bool, T)> convert)
        {
            return (argumentResult) =>
            {
                if (argumentResult.Parent is not OptionResult or)
                {
                    throw new NotSupportedException("The method should be only used with option.");
                }

                if (argumentResult.Tokens.Count == 0)
                {
                    if (or.IsImplicit)
                    {
                        if (parameter.DefaultValue != null)
                        {
                            (bool parsed, T value) = convert(parameter.DefaultValue);
                            if (parsed)
                            {
                                return value;
                            }
                            argumentResult.ErrorMessage = $"Cannot parse default value '{parameter.DefaultValue}' for option '{or.Token?.Value}' as expected type {typeof(T).Name}.";
                            //https://github.com/dotnet/command-line-api/blob/5eca6545a0196124cc1a66d8bd43db8945f1f1b7/src/System.CommandLine/Argument%7BT%7D.cs#L99-L113
                            //TODO: system-command-line can handle null.
                            return default!;
                        }
                        argumentResult.ErrorMessage = $"Default value for argument missing for option: {or.Token?.Value}.";
                        return default!;
                    }
                    if (parameter.DefaultIfOptionWithoutValue != null)
                    {
                        (bool parsed, T value) = convert(parameter.DefaultIfOptionWithoutValue);
                        if (parsed)
                        {
                            return value;
                        }
                        argumentResult.ErrorMessage = $"Cannot parse default if option without value '{parameter.DefaultIfOptionWithoutValue}' for option '{or.Token?.Value}' as expected type {typeof(T).Name}.";
                        return default!;
                    }
                    argumentResult.ErrorMessage = $"Required argument missing for option: {or.Token?.Value}.";
                    return default!;
                }
                else if (argumentResult.Tokens.Count == 1)
                {
                    (bool parsed, T value) = convert(argumentResult.Tokens[0].Value);
                    if (parsed)
                    {
                        return value;
                    }
                    argumentResult.ErrorMessage = $"Cannot parse argument '{argumentResult.Tokens[0].Value}' for option '{or.Token?.Value}' as expected type {typeof(T).Name}.";
                    return default!;
                }
                else
                {
                    argumentResult.ErrorMessage = $"Using more than 1 argument is not allowed for '{or.Token?.Value}', used: {argumentResult.Tokens.Count}.";
                    return default!;
                }
            };
        }

        private ParseArgument<string> GetParseChoiceArgument(CliTemplateParameter parameter)
        {
            return (argumentResult) =>
            {
                if (argumentResult.Parent is not OptionResult or)
                {
                    throw new NotSupportedException("The method should be only used with option.");
                }

                if (argumentResult.Tokens.Count == 0)
                {
                    if (or.IsImplicit)
                    {
                        if (string.IsNullOrWhiteSpace(parameter.DefaultValue))
                        {
                            if (TryConvertValueToChoice(parameter.DefaultValue, parameter, out string defaultValue, out string error))
                            {
                                return defaultValue;
                            }
                            argumentResult.ErrorMessage = $"Cannot parse default value '{parameter.DefaultValue}' for option '{or.Token?.Value}' as expected type 'choice': {error}.";
                            return string.Empty;
                        }
                        argumentResult.ErrorMessage = $"Default value for argument missing for option: {or.Token?.Value}.";
                        return string.Empty;
                    }
                    if (parameter.DefaultIfOptionWithoutValue != null)
                    {
                        if (TryConvertValueToChoice(parameter.DefaultIfOptionWithoutValue, parameter, out string defaultValue, out string error))
                        {
                            return defaultValue;
                        }
                        argumentResult.ErrorMessage = $"Cannot parse default if option without value '{parameter.DefaultIfOptionWithoutValue}' for option '{or.Token?.Value}' as expected type 'choice': {error}.";
                        return string.Empty;
                    }
                    argumentResult.ErrorMessage = $"Required argument missing for option: {or.Token?.Value}.";
                    return string.Empty;
                }
                else if (argumentResult.Tokens.Count == 1)
                {
                    if (TryConvertValueToChoice(argumentResult.Tokens[0].Value, parameter, out string value, out string error))
                    {
                        return value;
                    }
                    argumentResult.ErrorMessage = $"Cannot parse argument '{argumentResult.Tokens[0].Value}' for option '{or.Token?.Value}' as expected type 'choice': {error}.";
                    return string.Empty;
                }
                else
                {
                    argumentResult.ErrorMessage = $"Using more than 1 argument is not allowed for '{or.Token?.Value}', used: {argumentResult.Tokens.Count}.";
                    return string.Empty;
                }
            };
        }

        private (bool, string) ConvertValueToString(string? value)
        {
            return (true, value ?? string.Empty);
        }

        private (bool, long) ConvertValueToInt(string? value)
        {
            if (long.TryParse(value, out long result))
            {
                return (true, result);
            }
            return (false, default);
        }

        private (bool, double) ConvertValueToFloat(string? value)
        {
            if (Utils.ParserExtensions.DoubleTryParseСurrentOrInvariant(value, out double convertedFloat))
            {
                return (true, convertedFloat);
            }
            return (false, default);
        }

        private (bool, long) ConvertValueToHex(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return (false, default);
            }

            if (value.Length < 3)
            {
                return (false, default);
            }

            if (!string.Equals(value.Substring(0, 2), "0x", StringComparison.OrdinalIgnoreCase))
            {
                return (false, default);
            }

            if (long.TryParse(value.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long convertedHex))
            {
                return (true, convertedHex);
            }
            return (false, default);
        }

        private bool TryConvertValueToChoice(string? value, CliTemplateParameter parameter, out string parsedValue, out string error)
        {
            parsedValue = string.Empty;
            if (value == null)
            {
                error = "value is <null>";
                return false;
            }

            if (parameter.Choices == null)
            {
                error = "no choices are defined for parameter";
                return false;
            }

            foreach (string choiceValue in parameter.Choices.Keys)
            {
                if (string.Equals(choiceValue, value, StringComparison.OrdinalIgnoreCase))
                {
                    parsedValue = choiceValue;
                    error = string.Empty;
                    return true;
                }
            }
            error = $"value '{value}' is not allowed, allowed values are: {string.Join(",", parameter.Choices.Keys.Select(key => $"'{key}'"))}";
            return false;
        }
    }
}
