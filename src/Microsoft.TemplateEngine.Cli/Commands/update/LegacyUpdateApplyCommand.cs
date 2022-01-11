// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class LegacyUpdateApplyCommand : BaseUpdateCommand
    {
        public LegacyUpdateApplyCommand(
            NewCommand parentCommand,
            ITemplateEngineHost host,
            ITelemetryLogger logger,
            NewCommandCallbacks callbacks)
            : base(parentCommand, host, logger, callbacks, "--update-apply", SymbolStrings.Command_Legacy_Update_Check_Description)
        {
            this.IsHidden = true;
            parentCommand.AddNoLegacyUsageValidators(this, except: new Option[] { InteractiveOption, AddSourceOption });
        }

        internal override Option<bool> InteractiveOption => ParentCommand.InteractiveOption;

        internal override Option<IReadOnlyList<string>> AddSourceOption => ParentCommand.AddSourceOption;
    }
}
