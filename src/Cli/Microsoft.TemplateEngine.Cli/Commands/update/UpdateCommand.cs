// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class UpdateCommand : BaseUpdateCommand
    {
        public UpdateCommand(
                NewCommand parentCommand,
                Func<ParseResult, ITemplateEngineHost> hostBuilder)
            : base(parentCommand, hostBuilder, "update", SymbolStrings.Command_Update_Description)
        {
            parentCommand.AddNoLegacyUsageValidators(this);
            this.AddOption(CheckOnlyOption);
        }

        internal static Option<bool> CheckOnlyOption { get; } = new(new[] { "--check-only", "--dry-run" })
        {
            Description = SymbolStrings.Command_Update_Option_CheckOnly
        };
    }
}
