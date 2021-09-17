// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class UpdateCommand : BaseCommand<UpdateCommandArgs>
    {
        internal UpdateCommand(ITemplateEngineHost host, ITelemetryLogger logger, NewCommandCallbacks callbacks) : base(host, logger, callbacks, "update") { }

        protected override Task<NewCommandStatus> ExecuteAsync(UpdateCommandArgs args, IEngineEnvironmentSettings environmentSettings, InvocationContext context) => throw new NotImplementedException();

        protected override UpdateCommandArgs ParseContext(ParseResult parseResult) => throw new NotImplementedException();
    }

    internal class UpdateCommandArgs : GlobalArgs
    {
        public UpdateCommandArgs(ParseResult parseResult) : base(parseResult)
        {
        }
    }
}
