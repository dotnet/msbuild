// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class SearchCommand : BaseCommand<SearchCommandArgs>
    {
        internal SearchCommand(ITemplateEngineHost host, ITelemetryLogger logger, New3Callbacks callbacks) : base(host, logger, callbacks, "search", LocalizableStrings.SearchTemplatesCommand) { }

        protected override Task<New3CommandStatus> ExecuteAsync(SearchCommandArgs args, IEngineEnvironmentSettings environmentSettings, InvocationContext context) => throw new NotImplementedException();

        protected override SearchCommandArgs ParseContext(ParseResult parseResult) => throw new NotImplementedException();
    }

    internal class SearchCommandArgs : GlobalArgs
    {
        public SearchCommandArgs(ParseResult parseResult) : base(parseResult)
        {
        }
    }
}
