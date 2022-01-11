// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class LegacyListCommand : BaseListCommand
    {
        public LegacyListCommand(NewCommand parentCommand, ITemplateEngineHost host, ITelemetryLogger logger, NewCommandCallbacks callbacks)
            : base(parentCommand, host, logger, callbacks, "--list")
        {
            this.IsHidden = true;
            this.AddAlias("-l");
            AddValidator(ValidateParentCommandArguments);

            parentCommand.AddNoLegacyUsageValidators(this, except: Filters.Values.Concat(new Symbol[] { ColumnsAllOption, ColumnsOption, parentCommand.ShortNameArgument }).ToArray());
        }

        public override Option<bool> ColumnsAllOption => ParentCommand.ColumnsAllOption;

        public override Option<IReadOnlyList<string>> ColumnsOption => ParentCommand.ColumnsOption;

        protected override Option GetFilterOption(FilterOptionDefinition def)
        {
            return ParentCommand.LegacyFilters[def];
        }

        private string? ValidateParentCommandArguments(CommandResult commandResult)
        {
            var nameArgumentResult = commandResult.Children.FirstOrDefault(symbol => symbol.Symbol == this.NameArgument);
            if (nameArgumentResult == null)
            {
                return null;
            }
            return ParentCommand.ValidateShortNameArgumentIsNotUsed(commandResult);
        }
    }
}
