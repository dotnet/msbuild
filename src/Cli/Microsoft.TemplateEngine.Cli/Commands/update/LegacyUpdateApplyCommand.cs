// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class LegacyUpdateApplyCommand : BaseUpdateCommand
    {
        public LegacyUpdateApplyCommand(
            NewCommand parentCommand,
            Func<ParseResult, ITemplateEngineHost> hostBuilder)
            : base(parentCommand, hostBuilder, "--update-apply", SymbolStrings.Command_Legacy_Update_Check_Description)
        {
            this.IsHidden = true;
            parentCommand.AddNoLegacyUsageValidators(this, except: new Option[] { InteractiveOption, AddSourceOption });
        }

        internal override Option<bool> InteractiveOption => ParentCommand.InteractiveOption;

        internal override Option<string[]> AddSourceOption => ParentCommand.AddSourceOption;

        protected override Task<NewCommandStatus> ExecuteAsync(UpdateCommandArgs args, IEngineEnvironmentSettings environmentSettings, InvocationContext context)
        {
            PrintDeprecationMessage<LegacyUpdateApplyCommand, UpdateCommand>(args.ParseResult);
            return base.ExecuteAsync(args, environmentSettings, context);
        }
    }
}
