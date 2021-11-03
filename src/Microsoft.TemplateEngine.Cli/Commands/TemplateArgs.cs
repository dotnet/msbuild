// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine.Parsing;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class TemplateArgs
    {
        private readonly ParseResult _parseResult;
        private readonly TemplateCommand _command;
        private Dictionary<string, OptionResult> _templateOptions = new Dictionary<string, OptionResult>();

        public TemplateArgs(TemplateCommand command, CliTemplateInfo template, ParseResult parseResult)
        {
            _parseResult = parseResult ?? throw new ArgumentNullException(nameof(parseResult));
            _command = command ?? throw new ArgumentNullException(nameof(command));

            Name = parseResult.GetValueForOptionOrNull(command.NameOption);
            OutputPath = parseResult.GetValueForOptionOrNull(command.OutputOption);
            IsForceFlagSpecified = parseResult.GetValueForOption(command.ForceOption);
            IsDryRun = parseResult.GetValueForOption(command.DryRunOption);
            NoUpdateCheck = parseResult.GetValueForOption(command.NoUpdateCheckOption);
            AllowScripts = parseResult.GetValueForOption(command.AllowScriptsOption);

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

            foreach (var opt in command.TemplateOptions)
            {
                if (parseResult.FindResultFor(opt.Value) is { } result)
                {
                    _templateOptions[opt.Key] = result;
                }
            }
            Template = template ?? throw new ArgumentNullException(nameof(template));
            NewCommandName = parseResult.GetNewCommandName();
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
                return _templateOptions.Select(o => (o.Key, _parseResult.GetValueForOptionOrNull(o.Value.Option)))
                    .Where(kvp => kvp.Item2 != null)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Item2);
            }

        }

        internal string NewCommandName { get; private set; }

        public bool TryGetAliasForCanonicalName(string canonicalName, out string? alias)
        {
            if (_command.TemplateOptions.ContainsKey(canonicalName))
            {
                alias = _command.TemplateOptions[canonicalName].Aliases.First();
                return true;
            }
            alias = null;
            return false;
        }
    }
}
