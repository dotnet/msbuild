// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class InstantiateCommand : BaseCommand<InstantiateCommandArgs>
    {
        internal InstantiateCommand(ITemplateEngineHost host, ITelemetryLogger logger, New3Callbacks callbacks) : base(host, logger, callbacks) { }

        protected override Command CreateCommandAbstract() => throw new NotImplementedException();

        protected override Task<int> ExecuteAsync(InstantiateCommandArgs args, CancellationToken cancellationToken) => throw new NotImplementedException();

        protected override InstantiateCommandArgs ParseContext(InvocationContext context) => throw new NotImplementedException();
    }

    internal class InstantiateCommandArgs : GlobalArgs
    {
        public InstantiateCommandArgs(InvocationContext invocationContext) : base(invocationContext)
        {
        }
    }
}
