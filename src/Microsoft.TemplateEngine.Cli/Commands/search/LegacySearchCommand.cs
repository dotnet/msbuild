// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class LegacySearchCommand : BaseSearchCommand
    {
        public LegacySearchCommand(NewCommand parentCommand, Func<ParseResult, ITemplateEngineHost> hostBuilder, Func<ParseResult, ITelemetryLogger> telemetryLoggerBuilder, NewCommandCallbacks callbacks)
            : base(parentCommand, hostBuilder, telemetryLoggerBuilder, callbacks, "--search")
        {
            this.IsHidden = true;
            AddValidator(ValidateParentCommandArguments);

            parentCommand.AddNoLegacyUsageValidators(this, except: Filters.Values.Concat(new Symbol[] { ColumnsAllOption, ColumnsOption, parentCommand.ShortNameArgument }).ToArray());
        }

        public override Option<bool> ColumnsAllOption => ParentCommand.ColumnsAllOption;

        public override Option<string[]> ColumnsOption => ParentCommand.ColumnsOption;

        protected override Option GetFilterOption(FilterOptionDefinition def)
        {
            return ParentCommand.LegacyFilters[def];
        }

        private void ValidateParentCommandArguments(CommandResult commandResult)
        {
            var nameArgumentResult = commandResult.Children.FirstOrDefault(symbol => symbol.Symbol == this.NameArgument);
            if (nameArgumentResult == null)
            {
                return;
            }
            ParentCommand.ValidateShortNameArgumentIsNotUsed(commandResult);
        }
    }
}
