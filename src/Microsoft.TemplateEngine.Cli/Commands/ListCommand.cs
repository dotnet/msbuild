// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class ListCommand : BaseCommand<ListCommandArgs>
    {
        internal ListCommand(ITemplateEngineHost host, ITelemetryLogger logger, NewCommandCallbacks callbacks) : base(host, logger, callbacks, "list") { }

        protected override Task<NewCommandStatus> ExecuteAsync(ListCommandArgs args, IEngineEnvironmentSettings environmentSettings, InvocationContext context) => throw new NotImplementedException();

        protected override ListCommandArgs ParseContext(ParseResult parseResult) => throw new NotImplementedException();
    }

    internal class ListCommandArgs : GlobalArgs
    {
        public ListCommandArgs(ParseResult parseResult) : base(parseResult)
        {
        }
    }
}
