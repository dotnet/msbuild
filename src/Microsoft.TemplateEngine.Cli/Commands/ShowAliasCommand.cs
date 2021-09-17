// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class ShowAliasCommand : BaseCommand<ShowAliasCommandArgs>
    {
        internal ShowAliasCommand(ITemplateEngineHost host, ITelemetryLogger logger, NewCommandCallbacks callbacks) : base(host, logger, callbacks, "alias-show") { }

        protected override Task<NewCommandStatus> ExecuteAsync(ShowAliasCommandArgs args, IEngineEnvironmentSettings environmentSettings, InvocationContext context) => throw new NotImplementedException();

        protected override ShowAliasCommandArgs ParseContext(ParseResult parseResult) => throw new NotImplementedException();
    }

    internal class ShowAliasCommandArgs : GlobalArgs
    {
        public ShowAliasCommandArgs(ParseResult parseResult) : base(parseResult)
        {
        }
    }
}
