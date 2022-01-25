// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class TemplateCommandArgs : ICommandArgs
    {
        private readonly TemplateCommand _command;
        private Dictionary<string, OptionResult> _templateOptions = new Dictionary<string, OptionResult>();

        public TemplateCommandArgs(TemplateCommand command, BaseCommand parentCommand, ParseResult parseResult)
        {
            ParseResult = parseResult ?? throw new ArgumentNullException(nameof(parseResult));
            _command = command ?? throw new ArgumentNullException(nameof(command));
            ParentCommand = parentCommand ?? throw new ArgumentNullException(nameof(parentCommand));
            RootCommand = GetRootCommand(parentCommand);

            Name = parseResult.GetValueForOptionOrNull(command.NameOption);
            OutputPath = parseResult.GetValueForOptionOrNull(command.OutputOption);
            IsForceFlagSpecified = parseResult.GetValueForOption(command.ForceOption);
            IsDryRun = parseResult.GetValueForOption(command.DryRunOption);
            NoUpdateCheck = parseResult.GetValueForOption(command.NoUpdateCheckOption);

            if (command.LanguageOption != null)
            {
                Language = parseResult.GetValueForOptionOrNull(command.LanguageOption);
            }
            if (command.TypeOption != null)
            {
                Type = parseResult.GetValueForOptionOrNull(command.TypeOption);
            }
            if (command.BaselineOption != null)
            {
                BaselineName = parseResult.GetValueForOptionOrNull(command.BaselineOption);
            }
            if (command.AllowScriptsOption != null)
            {
                AllowScripts = parseResult.GetValueForOption(command.AllowScriptsOption);
            }

            foreach (var opt in command.TemplateOptions)
            {
                if (parseResult.FindResultFor(opt.Value.Option) is { } result)
                {
                    _templateOptions[opt.Key] = result;
                }
            }
            Template = command.Template;
        }

        public string? Name { get; }

        public string? OutputPath { get; }

        public bool IsForceFlagSpecified { get; }

        public string? Language { get; }

        public string? Type { get; }

        public string? BaselineName { get; }

        public bool IsDryRun { get; }

        public bool NoUpdateCheck { get; }

        public AllowRunScripts? AllowScripts { get; }

        public CliTemplateInfo Template { get; }

        public IReadOnlyDictionary<string, string?> TemplateParameters
        {
            get
            {
                return _templateOptions.Select(o => (o.Key, GetValueForOption(o.Key, o.Value)))
                    .Where(kvp => kvp.Item2 != null)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Item2);
            }

        }

        public ParseResult ParseResult { get; }

        public Command Command => _command;

        public NewCommand RootCommand { get; }

        public BaseCommand ParentCommand { get; }

        public bool TryGetAliasForCanonicalName(string canonicalName, out string? alias)
        {
            if (_command.TemplateOptions.ContainsKey(canonicalName))
            {
                alias = _command.TemplateOptions[canonicalName].Aliases[0];
                return true;
            }
            alias = null;
            return false;
        }

        private string? GetValueForOption(string parameterName, OptionResult optionResult)
        {
            //if default value is used, no need to return it - it will be populated in template engine edge instead.
            if (optionResult.IsImplicit)
            {
                return null;
            }

            var optionValue = optionResult.GetValueOrDefault();
            if (optionValue == null)
            {
                return null;
            }

            if (!Template.CliParameters.TryGetValue(parameterName, out CliTemplateParameter? parameter))
            {
                throw new InvalidOperationException($"Parameter {parameterName} is not defined for {Template.Identity}.");
            }
            if (parameter.Type == ParameterType.Hex && optionResult.Option.ValueType == typeof(long))
            {
                var intValue = (long)optionValue;
                return $"0x{intValue.ToString("X")}";
            }
            return optionValue.ToString();
        }

        private NewCommand GetRootCommand(BaseCommand command)
        {
            if (command is NewCommand newCommand)
            {
                return newCommand;
            }
            Command? currentCommand = command;
            while (currentCommand != null && currentCommand is not NewCommand)
            {
                currentCommand = currentCommand.Parents.OfType<Command>().SingleOrDefault();
            }
            return currentCommand as NewCommand ?? throw new Exception($"Command structure is not correct: {nameof(NewCommand)} is not found.");
        }
    }
}
