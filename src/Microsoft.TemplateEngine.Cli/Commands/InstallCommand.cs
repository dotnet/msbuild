// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Extensions;
using Microsoft.TemplateEngine.Cli.HelpAndUsage;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class InstallCommand : BaseInstallCommand
    {
        private readonly LegacyInstallCommand _legacyInstallCommand;

        public InstallCommand(
                LegacyInstallCommand legacyInstallCommand,
                ITemplateEngineHost host,
                ITelemetryLogger logger,
                NewCommandCallbacks callbacks)
            : base(host, logger, callbacks, "install")
        {
            AddValidator(ValidateLegacyUsage);
            _legacyInstallCommand = legacyInstallCommand;
        }

        private string? ValidateLegacyUsage(CommandResult symbolResult)
        {
            //if (symbolResult.Parent!.Children.Any((a)=> a // _legacyInstallCommand.InteractiveOption))
            //{
            //    return "We are doomed!";
            //}
            return null;
        }
    }

    internal class LegacyInstallCommand : BaseInstallCommand
    {
        public LegacyInstallCommand(NewCommand newCommand, ITemplateEngineHost host, ITelemetryLogger logger, NewCommandCallbacks callbacks)
            : base(host, logger, callbacks, "--install")
        {
            this.IsHidden = true;
            this.AddAlias("-i");
            AddSourceOption.AddAlias("--nuget-source");
            AddSourceOption.IsHidden = true;
            InteractiveOption.IsHidden = true;

            newCommand.AddOption(AddSourceOption);
            newCommand.AddOption(InteractiveOption);
        }

        public void AddOptionsToNewCommand(Command rootCommand)
        {
            rootCommand.Add(AddSourceOption);
            rootCommand.Add(InteractiveOption);
        }
    }

    internal abstract class BaseInstallCommand : BaseCommand<InstallCommandArgs>
    {
        internal BaseInstallCommand(ITemplateEngineHost host, ITelemetryLogger logger, NewCommandCallbacks callbacks, string commandName)
            : base(host, logger, callbacks, commandName)
        {
            this.AddArgument(NameArgument);
            this.AddOption(InteractiveOption);
            this.AddOption(AddSourceOption);
        }

        internal Argument<IReadOnlyList<string>> NameArgument { get; } = new("name")
        {
            Description = "Name of NuGet package or folder.",
            Arity = new ArgumentArity(1, 99)
        };

        internal Option<bool> InteractiveOption { get; } = new("--interactive")
        {
            Description = "When downloading enable NuGet interactive."
        };

        internal Option<IReadOnlyList<string>> AddSourceOption { get; } = new(new[] { "--add-source" })
        {
            Description = "Add NuGet source when looking for package.",
            AllowMultipleArgumentsPerToken = true,
            IsHidden = true
        };

        protected override Task<NewCommandStatus> ExecuteAsync(InstallCommandArgs args, IEngineEnvironmentSettings environmentSettings, InvocationContext context)
        {
            using TemplatePackageManager templatePackageManager = new TemplatePackageManager(environmentSettings);
            TemplateInformationCoordinator templateInformationCoordinator = new TemplateInformationCoordinator(
                environmentSettings,
                templatePackageManager,
                new TemplateCreator(environmentSettings),
                new HostSpecificDataLoader(environmentSettings),
                TelemetryLogger,
                environmentSettings.GetDefaultLanguage());

            TemplatePackageCoordinator templatePackageCoordinator = new TemplatePackageCoordinator(
                TelemetryLogger,
                environmentSettings,
                templatePackageManager,
                templateInformationCoordinator,
                environmentSettings.GetDefaultLanguage());

            return templatePackageCoordinator.EnterInstallFlowAsync(args, context.GetCancellationToken());
        }

        protected override InstallCommandArgs ParseContext(ParseResult parseResult)
        {
            return new InstallCommandArgs(this, parseResult);
        }
    }

    internal class InstallCommandArgs : GlobalArgs
    {
        public InstallCommandArgs(BaseInstallCommand installCommand, ParseResult parseResult) : base(installCommand, parseResult)
        {
            TemplatePackages = parseResult.ValueForArgument(installCommand.NameArgument) ?? throw new Exception("This shouldn't happen, we set ArgumentArity(1)...");
            Interactive = parseResult.ValueForOption(installCommand.InteractiveOption);
            AdditionalSources = parseResult.ValueForOption(installCommand.AddSourceOption);
        }

        public IReadOnlyList<string> TemplatePackages { get; }

        public bool Interactive { get; }

        public IReadOnlyList<string>? AdditionalSources { get; }
    }
}
