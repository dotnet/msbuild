// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class LegacyUninstallCommand : BaseUninstallCommand
    {
        public LegacyUninstallCommand(
            NewCommand parentCommand,
            Func<ParseResult, ITemplateEngineHost> hostBuilder,
            Func<ParseResult, ITelemetryLogger> telemetryLoggerBuilder)
            : base(hostBuilder, telemetryLoggerBuilder, "--uninstall")
        {
            this.IsHidden = true;
            this.AddAlias("-u");

            parentCommand.AddNoLegacyUsageValidators(this);
        }

        protected override Task<NewCommandStatus> ExecuteAsync(UninstallCommandArgs args, IEngineEnvironmentSettings environmentSettings, ITelemetryLogger telemetryLogger, InvocationContext context)
        {
            PrintDeprecationMessage<LegacyUninstallCommand, UninstallCommand>(args.ParseResult);
            return base.ExecuteAsync(args, environmentSettings, telemetryLogger, context);
        }
    }
}
