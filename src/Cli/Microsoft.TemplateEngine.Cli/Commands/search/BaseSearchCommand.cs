// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using System.CommandLine.Invocation;
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

            this.AddArgument(NameArgument);
            SetupTabularOutputOptions(this);
        }

        public virtual Option<bool> ColumnsAllOption { get; } = SharedOptionsFactory.CreateColumnsAllOption();

        public virtual Option<string[]> ColumnsOption { get; } = SharedOptionsFactory.CreateColumnsOption();

        public IReadOnlyDictionary<FilterOptionDefinition, Option> Filters { get; protected set; }

        internal static Argument<string> NameArgument { get; } = new("template-name")
        {
            Description = SymbolStrings.Command_Search_Argument_Name,
            Arity = new ArgumentArity(0, 1)
        };

        internal NewCommand ParentCommand { get; }

        protected override Task<NewCommandStatus> ExecuteAsync(
            SearchCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            InvocationContext context)
        {
            return CliTemplateSearchCoordinator.SearchForTemplateMatchesAsync(
                environmentSettings,
                templatePackageManager,
                args,
                environmentSettings.GetDefaultLanguage(),
                context.GetCancellationToken());
        }

        protected override SearchCommandArgs ParseContext(ParseResult parseResult)
        {
            return new SearchCommandArgs(this, parseResult);
        }
    }
}
