// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class LegacyUpdateCheckCommand : BaseUpdateCommand
    {
        public LegacyUpdateCheckCommand(
            NewCommand parentCommand,
            Func<ParseResult, ITemplateEngineHost> hostBuilder)
            : base(parentCommand, hostBuilder, "--update-check", SymbolStrings.Command_Update_Description)
        {
            this.IsHidden = true;
            parentCommand.AddNoLegacyUsageValidators(this, except: new Option[] { InteractiveOption, AddSourceOption });
        }

        internal override Option<bool> InteractiveOption => ParentCommand.InteractiveOption;

        internal override Option<string[]> AddSourceOption => ParentCommand.AddSourceOption;

        protected override Task<NewCommandStatus> ExecuteAsync(UpdateCommandArgs args, IEngineEnvironmentSettings environmentSettings, InvocationContext context)
        {
            PrintDeprecationMessage<LegacyUpdateCheckCommand, UpdateCommand>(args.ParseResult, additionalOption: UpdateCommand.CheckOnlyOption);

            return base.ExecuteAsync(args, environmentSettings, context);
        }
    }
}
