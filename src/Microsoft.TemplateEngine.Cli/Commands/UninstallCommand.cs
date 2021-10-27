// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class UninstallCommand : BaseUninstallCommand
    {
        public UninstallCommand(ITemplateEngineHost host, ITelemetryLogger logger, NewCommandCallbacks callbacks)
            : base(host, logger, callbacks, "uninstall")
        {
        }
    }

    internal class LegacyUninstallCommand : BaseUninstallCommand
    {
        public LegacyUninstallCommand(ITemplateEngineHost host, ITelemetryLogger logger, NewCommandCallbacks callbacks)
            : base(host, logger, callbacks, "--uninstall")
        {
            this.IsHidden = true;
            this.AddAlias("-u");
        }
    }

    internal class BaseUninstallCommand : BaseCommand<UninstallCommandArgs>
    {
        internal BaseUninstallCommand(ITemplateEngineHost host, ITelemetryLogger logger, NewCommandCallbacks callbacks, string commandName) : base(host, logger, callbacks, commandName, LocalizableStrings.UninstallHelp)
        {
            this.AddArgument(NameArgument);
        }

        internal Argument<IReadOnlyList<string>> NameArgument { get; } = new("name")
        {
            Description = "Name of NuGet package or folder to uninstall",
            Arity = new ArgumentArity(0, 99)
        };

        protected override async Task<NewCommandStatus> ExecuteAsync(UninstallCommandArgs args, IEngineEnvironmentSettings environmentSettings, InvocationContext context)
        {
            using TemplatePackageManager templatePackageManager = new TemplatePackageManager(environmentSettings);
            TemplatePackageCoordinator templatePackageCoordinator = new TemplatePackageCoordinator(
                TelemetryLogger,
                environmentSettings,
                templatePackageManager);

            return await templatePackageCoordinator.EnterUninstallFlowAsync(args, context.GetCancellationToken()).ConfigureAwait(false);
        }

        protected override UninstallCommandArgs ParseContext(ParseResult parseResult)
        {
            return new UninstallCommandArgs(this, parseResult);
        }
    }

    internal class UninstallCommandArgs : GlobalArgs
    {
        public UninstallCommandArgs(BaseUninstallCommand uninstallCommand, ParseResult parseResult) : base(uninstallCommand, parseResult)
        {
            TemplatePackages = parseResult.GetValueForArgument(uninstallCommand.NameArgument) ?? Array.Empty<string>();

            //workaround for --install source1 --install source2 case
            if (uninstallCommand is LegacyUninstallCommand && uninstallCommand.Aliases.Any(alias => TemplatePackages.Contains(alias)))
            {
                TemplatePackages = TemplatePackages.Where(package => !uninstallCommand.Aliases.Contains(package)).ToList();
            }
        }

        public IReadOnlyList<string> TemplatePackages { get; }
    }
}
