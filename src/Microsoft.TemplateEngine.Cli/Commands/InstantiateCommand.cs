// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class InstantiateCommand : BaseCommand<InstantiateCommandArgs>
    {
        internal InstantiateCommand(ITemplateEngineHost host, ITelemetryLogger logger, NewCommandCallbacks callbacks) : base(host, logger, callbacks, "create")
        {
            InstantiateCommandArgs.AddToCommand(this);

        }

        protected override Task<NewCommandStatus> ExecuteAsync(InstantiateCommandArgs args, IEngineEnvironmentSettings environmentSettings, InvocationContext context) => throw new NotImplementedException();

        protected override InstantiateCommandArgs ParseContext(ParseResult parseResult) => throw new NotImplementedException();
    }

    internal class InstantiateCommandArgs : GlobalArgs
    {
        public InstantiateCommandArgs(InstantiateCommand command, ParseResult parseResult) : base(command, parseResult)
        {
            OutputPath = parseResult.GetValueForOption(OutputPathOption);
        }

        public string? OutputPath { get; }

        private static Option<string> OutputPathOption { get; } = new(new[] { "-o", "--output" })
        {
            Description = "Location to place the generated output. The default is the current directory."
        };

        internal static void AddToCommand(Command command)
        {
            command.AddOption(OutputPathOption);
        }

    }
}
