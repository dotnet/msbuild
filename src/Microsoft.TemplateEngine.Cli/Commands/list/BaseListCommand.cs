// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
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

        internal BaseListCommand(
            NewCommand parentCommand,
            Func<ParseResult, ITemplateEngineHost> hostBuilder,
            Func<ParseResult, ITelemetryLogger> telemetryLoggerBuilder,
            NewCommandCallbacks callbacks,
            string commandName)
            : base(hostBuilder, telemetryLoggerBuilder, callbacks, commandName, SymbolStrings.Command_List_Description)
        {
            ParentCommand = parentCommand;
            Filters = SetupFilterOptions(SupportedFilters);

            this.AddArgument(NameArgument);
            SetupTabularOutputOptions(this);
        }

        public virtual Option<bool> ColumnsAllOption { get; } = SharedOptionsFactory.CreateColumnsAllOption();

        public virtual Option<string[]> ColumnsOption { get; } = SharedOptionsFactory.CreateColumnsOption();

        public IReadOnlyDictionary<FilterOptionDefinition, Option> Filters { get; protected set; }

        internal Argument<string> NameArgument { get; } = new("name")
        {
            Description = SymbolStrings.Command_List_Argument_Name,
            Arity = new ArgumentArity(0, 1)
        };

        internal NewCommand ParentCommand { get; }

        protected override async Task<NewCommandStatus> ExecuteAsync(
            ListCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            ITelemetryLogger telemetryLogger,
            InvocationContext context)
        {
            using TemplatePackageManager templatePackageManager = new TemplatePackageManager(environmentSettings);
            TemplateListCoordinator templateListCoordinator = new TemplateListCoordinator(
                environmentSettings,
                templatePackageManager,
                new HostSpecificDataLoader(environmentSettings),
                telemetryLogger);

            //we need to await, otherwise templatePackageManager will be disposed.
            return await templateListCoordinator.DisplayTemplateGroupListAsync(args, default).ConfigureAwait(false);
        }

        protected override ListCommandArgs ParseContext(ParseResult parseResult) => new(this, parseResult);

    }
}
