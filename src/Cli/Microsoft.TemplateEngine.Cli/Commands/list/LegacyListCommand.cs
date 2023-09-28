// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class LegacyListCommand : BaseListCommand
    {
        public LegacyListCommand(
            NewCommand parentCommand,
            Func<ParseResult, ITemplateEngineHost> hostBuilder)
            : base(parentCommand, hostBuilder, "--list")
        {
            Hidden = true;
            Aliases.Add("-l");
            Validators.Add(ValidateParentCommandArguments);

            parentCommand.AddNoLegacyUsageValidators(this, except: Filters.Values.Concat(new CliSymbol[] { ColumnsAllOption, ColumnsOption, NewCommand.ShortNameArgument }).ToArray());
        }

        public override CliOption<bool> ColumnsAllOption => ParentCommand.ColumnsAllOption;

        public override CliOption<string[]> ColumnsOption => ParentCommand.ColumnsOption;

        protected override CliOption GetFilterOption(FilterOptionDefinition def)
        {
            return ParentCommand.LegacyFilters[def];
        }

        protected override Task<NewCommandStatus> ExecuteAsync(ListCommandArgs args, IEngineEnvironmentSettings environmentSettings, TemplatePackageManager templatePackageManager, ParseResult parseResult, CancellationToken cancellationToken)
        {
            PrintDeprecationMessage<LegacyListCommand, ListCommand>(args.ParseResult);
            return base.ExecuteAsync(args, environmentSettings, templatePackageManager, parseResult, cancellationToken);
        }

        private void ValidateParentCommandArguments(CommandResult commandResult)
        {
            var nameArgumentResult = commandResult.Children.FirstOrDefault(
                symbol => symbol is ArgumentResult argumentResult && argumentResult.Argument == NameArgument);
            if (nameArgumentResult == null)
            {
                return;
            }
            ParentCommand.ValidateShortNameArgumentIsNotUsed(commandResult);
        }
    }
}
