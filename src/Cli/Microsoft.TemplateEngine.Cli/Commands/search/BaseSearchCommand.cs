// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.TemplateSearch;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class BaseSearchCommand : BaseCommand<SearchCommandArgs>, IFilterableCommand, ITabularOutputCommand
    {
        internal static readonly IReadOnlyList<FilterOptionDefinition> SupportedFilters = new List<FilterOptionDefinition>()
        {
            FilterOptionDefinition.AuthorFilter,
            FilterOptionDefinition.BaselineFilter,
            FilterOptionDefinition.LanguageFilter,
            FilterOptionDefinition.TypeFilter,
            FilterOptionDefinition.TagFilter,
            FilterOptionDefinition.PackageFilter
        };

        internal BaseSearchCommand(
            NewCommand parentCommand,
            Func<ParseResult, ITemplateEngineHost> hostBuilder,
            string commandName)
            : base(hostBuilder, commandName, SymbolStrings.Command_Search_Description)
        {
            ParentCommand = parentCommand;
            Filters = SetupFilterOptions(SupportedFilters);

            this.Arguments.Add(NameArgument);
            SetupTabularOutputOptions(this);
        }

        public virtual CliOption<bool> ColumnsAllOption { get; } = SharedOptionsFactory.CreateColumnsAllOption();

        public virtual CliOption<string[]> ColumnsOption { get; } = SharedOptionsFactory.CreateColumnsOption();

        public IReadOnlyDictionary<FilterOptionDefinition, CliOption> Filters { get; protected set; }

        internal static CliArgument<string> NameArgument { get; } = new("template-name")
        {
            Description = SymbolStrings.Command_Search_Argument_Name,
            Arity = new ArgumentArity(0, 1)
        };

        internal NewCommand ParentCommand { get; }

        protected override Task<NewCommandStatus> ExecuteAsync(
            SearchCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            ParseResult parseResult,
            CancellationToken cancellationToken)
        {
            return CliTemplateSearchCoordinator.SearchForTemplateMatchesAsync(
                environmentSettings,
                templatePackageManager,
                args,
                environmentSettings.GetDefaultLanguage(),
                cancellationToken);
        }

        protected override SearchCommandArgs ParseContext(ParseResult parseResult)
        {
            return new SearchCommandArgs(this, parseResult);
        }
    }
}
