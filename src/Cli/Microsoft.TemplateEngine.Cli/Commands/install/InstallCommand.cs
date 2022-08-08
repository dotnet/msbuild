// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class InstallCommand : BaseInstallCommand
    {
        public InstallCommand(
                NewCommand parentCommand,
                Func<ParseResult, ITemplateEngineHost> hostBuilder,
                Func<ParseResult, ITelemetryLogger> telemetryLoggerBuilder)
            : base(parentCommand, hostBuilder, telemetryLoggerBuilder, "install")
        {
            parentCommand.AddNoLegacyUsageValidators(this);
        }
    }
}
