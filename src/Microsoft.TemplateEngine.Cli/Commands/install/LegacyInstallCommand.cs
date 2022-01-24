// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class LegacyInstallCommand : BaseInstallCommand
    {
        public LegacyInstallCommand(NewCommand parentCommand, Func<ParseResult, ITemplateEngineHost> hostBuilder, Func<ParseResult, ITelemetryLogger> telemetryLoggerBuilder, NewCommandCallbacks callbacks)
            : base(parentCommand, hostBuilder, telemetryLoggerBuilder, callbacks, "--install")
        {
            this.IsHidden = true;
            this.AddAlias("-i");

            parentCommand.AddNoLegacyUsageValidators(this, except: new Option[] { InteractiveOption, AddSourceOption });
        }

        internal override Option<bool> InteractiveOption => ParentCommand.InteractiveOption;

        internal override Option<string[]> AddSourceOption => ParentCommand.AddSourceOption;
    }
}
