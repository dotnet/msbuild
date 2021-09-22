// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class AliasAddCommand : BaseCommand<AliasAddCommandArgs>
    {
        internal AliasAddCommand(ITemplateEngineHost host, ITelemetryLogger logger, NewCommandCallbacks callbacks) : base(host, logger, callbacks, "add") { }

        protected override Task<NewCommandStatus> ExecuteAsync(AliasAddCommandArgs args, IEngineEnvironmentSettings environmentSettings, InvocationContext context) => throw new NotImplementedException();

        protected override AliasAddCommandArgs ParseContext(ParseResult parseResult) => new(parseResult);
    }

    internal class AliasAddCommandArgs : GlobalArgs
    {
        public AliasAddCommandArgs(ParseResult parseResult) : base(parseResult)
        {
        }
    }
}
