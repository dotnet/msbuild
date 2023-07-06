// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal abstract class BaseInstallCommand : BaseCommand<InstallCommandArgs>
    {
        internal BaseInstallCommand(
            NewCommand parentCommand,
            Func<ParseResult, ITemplateEngineHost> hostBuilder,
            string commandName)
            : base(hostBuilder, commandName, SymbolStrings.Command_Install_Description)
        {
            ParentCommand = parentCommand;
            this.Arguments.Add(NameArgument);
            this.Options.Add(InteractiveOption);
            this.Options.Add(AddSourceOption);
            this.Options.Add(ForceOption);
        }

        internal static CliArgument<string[]> NameArgument { get; } = new("package")
        {
            Description = SymbolStrings.Command_Install_Argument_Package,
            Arity = new ArgumentArity(1, 99)
        };

        internal static CliOption<bool> ForceOption { get; } = SharedOptionsFactory.CreateForceOption().WithDescription(SymbolStrings.Option_Install_Force);

        internal virtual CliOption<bool> InteractiveOption { get; } = SharedOptions.InteractiveOption;

        internal virtual CliOption<string[]> AddSourceOption { get; } = SharedOptionsFactory.CreateAddSourceOption();

        protected NewCommand ParentCommand { get; }

        protected override Task<NewCommandStatus> ExecuteAsync(
            InstallCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            ParseResult parseResult,
            CancellationToken cancellationToken)
        {
            TemplatePackageCoordinator templatePackageCoordinator = new(environmentSettings, templatePackageManager);
            return templatePackageCoordinator.EnterInstallFlowAsync(args, cancellationToken);
        }

        protected override InstallCommandArgs ParseContext(ParseResult parseResult)
        {
            return new InstallCommandArgs(this, parseResult);
        }
    }
}
