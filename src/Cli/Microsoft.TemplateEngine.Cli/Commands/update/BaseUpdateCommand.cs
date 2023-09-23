// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class BaseUpdateCommand : BaseCommand<UpdateCommandArgs>
    {
        internal BaseUpdateCommand(
            NewCommand parentCommand,
            Func<ParseResult, ITemplateEngineHost> hostBuilder,
            string commandName,
            string description)
            : base(hostBuilder, commandName, description)
        {
            ParentCommand = parentCommand;
            Options.Add(InteractiveOption);
            Options.Add(AddSourceOption);
        }

        internal virtual CliOption<bool> InteractiveOption { get; } = SharedOptionsFactory.CreateInteractiveOption();

        internal virtual CliOption<string[]> AddSourceOption { get; } = SharedOptionsFactory.CreateAddSourceOption();

        protected NewCommand ParentCommand { get; }

        protected override Task<NewCommandStatus> ExecuteAsync(
            UpdateCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            ParseResult context,
            CancellationToken cancellationToken)
        {
            TemplatePackageCoordinator templatePackageCoordinator = new(environmentSettings, templatePackageManager);

            return templatePackageCoordinator.EnterUpdateFlowAsync(args, cancellationToken);
        }

        protected override UpdateCommandArgs ParseContext(ParseResult parseResult) => new(this, parseResult);
    }
}
