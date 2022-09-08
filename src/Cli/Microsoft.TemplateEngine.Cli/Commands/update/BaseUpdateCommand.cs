// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using System.CommandLine.Invocation;
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
            this.AddOption(InteractiveOption);
            this.AddOption(AddSourceOption);
        }

        internal virtual Option<bool> InteractiveOption { get; } = SharedOptionsFactory.CreateInteractiveOption();

        internal virtual Option<string[]> AddSourceOption { get; } = SharedOptionsFactory.CreateAddSourceOption();

        protected NewCommand ParentCommand { get; }

        protected override async Task<NewCommandStatus> ExecuteAsync(
            UpdateCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            InvocationContext context)
        {
            using TemplatePackageManager templatePackageManager = new TemplatePackageManager(environmentSettings);
            TemplatePackageCoordinator templatePackageCoordinator = new TemplatePackageCoordinator(
                environmentSettings,
                templatePackageManager);

            //we need to await, otherwise templatePackageManager will be disposed.
            return await templatePackageCoordinator.EnterUpdateFlowAsync(args, context.GetCancellationToken()).ConfigureAwait(false);
        }

        protected override UpdateCommandArgs ParseContext(ParseResult parseResult) => new(this, parseResult);
    }
}
