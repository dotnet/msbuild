// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

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

        protected override Task<NewCommandStatus> ExecuteAsync(UpdateCommandArgs args, IEngineEnvironmentSettings environmentSettings, TemplatePackageManager templatePackageManager, InvocationContext context)
        {
            PrintDeprecationMessage<LegacyUpdateApplyCommand, UpdateCommand>(args.ParseResult);
            return base.ExecuteAsync(args, environmentSettings, templatePackageManager, context);
        }
    }
}
