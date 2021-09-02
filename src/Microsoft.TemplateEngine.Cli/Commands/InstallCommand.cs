// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class InstallCommand : BaseCommand<InstallCommandArgs>
    {
        internal InstallCommand(ITemplateEngineHost host, ITelemetryLogger logger, New3Callbacks callbacks) : base(host, logger, callbacks) { }

        protected override Command CreateCommandAbstract()
        {
            var command = new Command("install");
            InstallCommandArgs.AddToCommand(command);
            return command;
        }

        protected override Task<int> ExecuteAsync(InstallCommandArgs args, CancellationToken cancellationToken = default)
        {
            IEngineEnvironmentSettings environmentSettings = GetEnvironmentSettings(args, outputPath: null);
            throw new NotImplementedException();
        }

        protected override InstallCommandArgs ParseContext(InvocationContext context) => new(context);
    }

    internal class InstallCommandArgs : GlobalArgs
    {
        public InstallCommandArgs(InvocationContext invocationContext)
            : base(invocationContext)
        {
            Name = invocationContext.ParseResult.ValueForArgument(NameArgumnet) ?? throw new Exception("This shouldn't happen, we set ArgumentArity(1)...");
            Interactive = invocationContext.ParseResult.ValueForOption(InteractiveOption);
            AddSource = invocationContext.ParseResult.ValueForOption(AddSourceOption);
        }

        public string[] Name { get; }

        public bool Interactive { get; }

        public string? AddSource { get; }

        private static Argument<string[]> NameArgumnet { get; } = new("name")
        {
            Description = "Name of NuGet package or folder.",
            Arity = new ArgumentArity(1, 99)
        };

        private static Option<bool> InteractiveOption { get; } = new("--interactive")
        {
            Description = "When downloading enable NuGet interactive."
        };

        private static Option<string> AddSourceOption { get; } = new("--add-source")
        {
            Description = "Add NuGet source when looking for package.",
            AllowMultipleArgumentsPerToken = true
        };

        internal static void AddToCommand(Command command)
        {
            command.AddArgument(NameArgumnet);
            command.AddOption(InteractiveOption);
            command.AddOption(AddSourceOption);
        }
    }
}
