// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Extensions;
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
            ITemplateEngineHost host,
            ITelemetryLogger logger,
            NewCommandCallbacks callbacks,
            string commandName)
            : base(host, logger, callbacks, commandName, SymbolStrings.Command_Search_Description)
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
            Description = SymbolStrings.Command_Search_Argument_Name,
            Arity = new ArgumentArity(0, 1)
        };

        internal NewCommand ParentCommand { get; }

        protected override async Task<NewCommandStatus> ExecuteAsync(SearchCommandArgs args, IEngineEnvironmentSettings environmentSettings, InvocationContext context)
        {
            using TemplatePackageManager templatePackageManager = new TemplatePackageManager(environmentSettings);
            //we need to await, otherwise templatePackageManager will be disposed.
            return await CliTemplateSearchCoordinator.SearchForTemplateMatchesAsync(
                environmentSettings,
                templatePackageManager,
                args,
                environmentSettings.GetDefaultLanguage(),
                context.GetCancellationToken()).ConfigureAwait(false);
        }

        protected override SearchCommandArgs ParseContext(ParseResult parseResult)
        {
            return new SearchCommandArgs(this, parseResult);
        }
    }
}
