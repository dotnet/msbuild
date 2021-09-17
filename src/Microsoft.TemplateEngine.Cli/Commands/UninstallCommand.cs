// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class UninstallCommand : BaseCommand<UninstallCommandArgs>
    {
        internal UninstallCommand(ITemplateEngineHost host, ITelemetryLogger logger, NewCommandCallbacks callbacks) : base(host, logger, callbacks, "uninstall") { }

        protected override Task<NewCommandStatus> ExecuteAsync(UninstallCommandArgs args, IEngineEnvironmentSettings environmentSettings, InvocationContext context) => throw new NotImplementedException();

        protected override UninstallCommandArgs ParseContext(ParseResult parseResult) => throw new NotImplementedException();
    }

    internal class UninstallCommandArgs : GlobalArgs
    {
        public UninstallCommandArgs(ParseResult parseResult) : base(parseResult)
        {
        }
    }
}
