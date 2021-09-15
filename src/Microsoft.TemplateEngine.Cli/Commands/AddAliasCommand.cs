// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class AddAliasCommand : BaseCommandHandler<AddAliasCommandArgs>
    {
        internal AddAliasCommand(ITemplateEngineHost host, ITelemetryLogger logger, New3Callbacks callbacks) : base(host, logger, callbacks) { }

        protected override Command CreateCommandAbstract() => throw new NotImplementedException();

        protected override Task<New3CommandStatus> ExecuteAsync(AddAliasCommandArgs args, IEngineEnvironmentSettings environmentSettings, InvocationContext context) => throw new NotImplementedException();

        protected override AddAliasCommandArgs ParseContext(ParseResult parseResult) => new(parseResult);
    }

    internal class AddAliasCommandArgs : GlobalArgs
    {
        public AddAliasCommandArgs(ParseResult parseResult) : base(parseResult)
        {
        }
    }
}
