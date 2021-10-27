// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class ListCommand : BaseListCommand
    {
        public ListCommand(
                NewCommand parentCommand,
                ITemplateEngineHost host,
                ITelemetryLogger logger,
                NewCommandCallbacks callbacks)
            : base(parentCommand, host, logger, callbacks, "list")
        {
            foreach (KeyValuePair<FilterOptionDefinition, Option> legacyOption in parentCommand.LegacyFilters)
            {
                if (SupportedFilters.Contains(legacyOption.Key))
                {
                    AddValidator(symbolResult => ValidateOptionUsageInParent(symbolResult, legacyOption.Value));
                }
            }
            AddValidator(symbolResult => ValidateOptionUsageInParent(symbolResult, parentCommand.ColumnsAllOption));
            AddValidator(symbolResult => ValidateOptionUsageInParent(symbolResult, parentCommand.ColumnsOption));
            AddValidator(symbolResult => ValidateArgumentUsageInParent(symbolResult, parentCommand.ShortNameArgument));
        }
    }

    internal class LegacyListCommand : BaseListCommand
    {
        public LegacyListCommand(NewCommand newCommand, ITemplateEngineHost host, ITelemetryLogger logger, NewCommandCallbacks callbacks)
            : base(newCommand, host, logger, callbacks, "--list")
        {
            this.IsHidden = true;
            this.AddAlias("-l");
            AddValidator(ValidateParentCommandArgumentIsNotUsed);
        }

        public override Option<bool> ColumnsAllOption => ParentCommand.ColumnsAllOption;

        public override Option<IReadOnlyList<string>> ColumnsOption => ParentCommand.ColumnsOption;

        protected override Option GetFilterOption(FilterOptionDefinition def)
        {
            return ParentCommand.LegacyFilters[def];
        }

        private string? ValidateParentCommandArgumentIsNotUsed(SymbolResult symbolResult)
        {
            CommandResult commandResult = symbolResult as CommandResult ?? throw new Exception("Validator should be used with command");
            var nameArgumentResult = commandResult.Children.FirstOrDefault(symbol => symbol.Symbol == this.NameArgument);
            if (nameArgumentResult == null)
            {
                return null;
            }
            var newCommandArgument = commandResult.Parent?.Children.FirstOrDefault(symbol => symbol.Symbol == ParentCommand.ShortNameArgument) as ArgumentResult;
            if (newCommandArgument != null)
            {
                return $"Invalid command syntax: argument '{newCommandArgument.Tokens[0].Value}' should be used after '{symbolResult.Symbol.Name}'.";
            }
            return null;
        }
    }

    internal class BaseListCommand : BaseCommand<ListCommandArgs>, IFilterableCommand, ITabularOutputCommand
    {
        internal static readonly IReadOnlyList<FilterOptionDefinition> SupportedFilters = new List<FilterOptionDefinition>()
        {
            FilterOptionDefinition.AuthorFilter,
            FilterOptionDefinition.BaselineFilter,
            FilterOptionDefinition.LanguageFilter,
            FilterOptionDefinition.TypeFilter,
            FilterOptionDefinition.TagFilter
        };

        internal BaseListCommand(NewCommand parentCommand, ITemplateEngineHost host, ITelemetryLogger logger, NewCommandCallbacks callbacks, string commandName) : base(host, logger, callbacks, commandName, LocalizableStrings.ListsTemplates)
        {
            ParentCommand = parentCommand;
            Filters = SetupFilterOptions(SupportedFilters);

            this.AddArgument(NameArgument);
            SetupTabularOutputOptions(this);
        }

        public virtual Option<bool> ColumnsAllOption { get; } = SharedOptionsFactory.CreateColumnsAllOption();

        public virtual Option<IReadOnlyList<string>> ColumnsOption { get; } = SharedOptionsFactory.CreateColumnsOption();

        public IReadOnlyDictionary<FilterOptionDefinition, Option> Filters { get; protected set; }

        internal Argument<string> NameArgument { get; } = new("name")
        {
            Description = "Name of template to filter for",
            Arity = new ArgumentArity(0, 1)
        };

        internal NewCommand ParentCommand { get; }

        protected override async Task<NewCommandStatus> ExecuteAsync(ListCommandArgs args, IEngineEnvironmentSettings environmentSettings, InvocationContext context)
        {
            using TemplatePackageManager templatePackageManager = new TemplatePackageManager(environmentSettings);
            TemplateListCoordinator templateListCoordinator = new TemplateListCoordinator(
                environmentSettings,
                templatePackageManager,
                new TemplateCreator(environmentSettings),
                new HostSpecificDataLoader(environmentSettings),
                TelemetryLogger);

            //TODO: consider putting TemplatePackageManager into base class, so no need to await here for dispose.
            return await templateListCoordinator.DisplayTemplateGroupListAsync(args, default).ConfigureAwait(false);
        }

        protected override ListCommandArgs ParseContext(ParseResult parseResult) => new(this, parseResult);

    }

    internal class ListCommandArgs : BaseFilterableArgs, ITabularOutputArgs
    {
        internal ListCommandArgs(BaseListCommand command, ParseResult parseResult) : base(command, parseResult)
        {
            string? nameCriteria = parseResult.GetValueForArgument(command.NameArgument);
            if (!string.IsNullOrWhiteSpace(nameCriteria))
            {
                ListNameCriteria = nameCriteria;
            }
            // for legacy case new command argument is also accepted
            else if (command is LegacyListCommand legacySearchCommand)
            {
                string? newCommandArgument = parseResult.GetValueForArgument(legacySearchCommand.ParentCommand.ShortNameArgument);
                if (!string.IsNullOrWhiteSpace(newCommandArgument))
                {
                    ListNameCriteria = newCommandArgument;
                }
            }
            (DisplayAllColumns, ColumnsToDisplay) = ParseTabularOutputSettings(command, parseResult);
            if (AppliedFilters.Contains(FilterOptionDefinition.LanguageFilter))
            {
                Language = GetFilterValue(FilterOptionDefinition.LanguageFilter);
            }
        }

        public bool DisplayAllColumns { get; }

        public IReadOnlyList<string>? ColumnsToDisplay { get; }

        internal string? ListNameCriteria { get; }

        internal string? Language { get; }
    }
}
