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
    internal class InstallCommand : BaseCommand<InstallCommandArgs>
    {
        internal InstallCommand(ITemplateEngineHost host, ITelemetryLogger logger, NewCommandCallbacks callbacks, string commandName)
            : base(host, logger, callbacks, commandName)
        {
        }

        internal static InstallCommand GetCommand(ITemplateEngineHost host, ITelemetryLogger logger, NewCommandCallbacks callbacks)
        {
            InstallCommand command = new InstallCommand(host, logger, callbacks, "install");
            InstallCommandArgs.AddToCommand(command);
            return command;
        }

        internal static InstallCommand GetLegacyCommand(ITemplateEngineHost host, ITelemetryLogger logger, NewCommandCallbacks callbacks)
        {
            InstallCommand command = new InstallCommand(host, logger, callbacks, "--install");
            command.IsHidden = true;
            command.AddAlias("-i");
            InstallCommandArgs.AddToCommand(command, legacy: true);
            return command;
        }

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
            return new InstallCommandArgs(parseResult, IsLegacyCommand());
        }

        private bool IsLegacyCommand()
        {
           return this.Name == "--install";
        }
    }

    internal class InstallCommandArgs : GlobalArgs
    {
        public InstallCommandArgs(ParseResult parseResult, bool legacy = false) : base(parseResult)
        {
            if (legacy)
            {
                TemplatePackages = parseResult.ValueForArgument(Legacy.NameArgument) ?? throw new Exception("This shouldn't happen, we set ArgumentArity(1)...");
                Interactive = parseResult.ValueForOption(Legacy.InteractiveOption);
                AdditionalSources = parseResult.ValueForOption(Legacy.AddSourceOption);
                return;
            }
            TemplatePackages = parseResult.ValueForArgument(NameArgument) ?? throw new Exception("This shouldn't happen, we set ArgumentArity(1)...");
            Interactive = parseResult.ValueForOption(InteractiveOption);
            AdditionalSources = parseResult.ValueForOption(AddSourceOption);
        }

        public IReadOnlyList<string> TemplatePackages { get; }

        public bool Interactive { get; }

        public IReadOnlyList<string>? AdditionalSources { get; }

        private static Argument<IReadOnlyList<string>> NameArgument { get; } = new("name")
        {
            Description = "Name of NuGet package or folder.",
            Arity = new ArgumentArity(1, 99)
        };

        private static Option<bool> InteractiveOption { get; } = new("--interactive")
        {
            Description = "When downloading enable NuGet interactive."
        };

        private static Option<IReadOnlyList<string>> AddSourceOption { get; } = new(new[] { "--add-source" })
        {
            Description = "Add NuGet source when looking for package.",
            AllowMultipleArgumentsPerToken = true,
        };

        internal static void AddLegacyOptionsToCommand(Command command)
        {
            command.AddOption(Legacy.InteractiveOption);
            command.AddOption(Legacy.AddSourceOption);
        }

        internal static void AddToCommand(Command command, bool legacy = false)
        {
            if (legacy)
            {
                command.AddArgument(Legacy.NameArgument);
                AddLegacyOptionsToCommand(command);
                return;
            }

            command.AddArgument(NameArgument);
            command.AddOption(InteractiveOption);
            command.AddOption(AddSourceOption);
        }

        private static class Legacy
        {
            internal static Argument<IReadOnlyList<string>> NameArgument { get; } = new("name")
            {
                Description = "Name of NuGet package or folder.",
                Arity = new ArgumentArity(1, 99),
                IsHidden = true
            };

            internal static Option<bool> InteractiveOption { get; } = new("--interactive")
            {
                Description = "When downloading enable NuGet interactive.",
                IsHidden = true,
            };

            internal static Option<IReadOnlyList<string>> AddSourceOption { get; } = new(new[] { "--add-source" })
            {
                Description = "Add NuGet source when looking for package.",
                AllowMultipleArgumentsPerToken = true,
                IsHidden = true,
            };
        }
    }
}
