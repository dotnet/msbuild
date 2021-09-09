// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Extensions;
using Microsoft.TemplateEngine.Cli.HelpAndUsage;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class LegacyInstallCommand : InstallCommand
    {
        internal LegacyInstallCommand(ITemplateEngineHost host, ITelemetryLogger logger, New3Callbacks callbacks) : base(host, logger, callbacks) { }

        protected override Command CreateCommandAbstract()
        {
            var command = new Command("--install");
            command.IsHidden = true;
            command.AddAlias("-i");
            InstallCommandArgs.AddToCommand(command);
            return command;

        }
    }

    internal class InstallCommand : BaseCommandHandler<InstallCommandArgs>
    {
        internal InstallCommand(ITemplateEngineHost host, ITelemetryLogger logger, New3Callbacks callbacks) : base(host, logger, callbacks) { }

        protected override Command CreateCommandAbstract()
        {
            var command = new Command("install");
            InstallCommandArgs.AddToCommand(command);
            return command;
        }

        protected override Task<New3CommandStatus> ExecuteAsync(InstallCommandArgs args, IEngineEnvironmentSettings environmentSettings, CancellationToken cancellationToken)
        {
            TemplatePackageManager templatePackageManager = new TemplatePackageManager(environmentSettings);
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

            return templatePackageCoordinator.EnterInstallFlowAsync(args, cancellationToken);
        }

        protected override InstallCommandArgs ParseContext(InvocationContext context) => new(context);
    }

    internal class LegacyInstallCommandArgs : GlobalArgs
    {
        public LegacyInstallCommandArgs(InvocationContext invocationContext)
            : base(invocationContext)
        {
            TemplatePackages = invocationContext.ParseResult.ValueForArgument(NameArgument) ?? throw new Exception("This shouldn't happen, we set ArgumentArity(1)...");
            Interactive = invocationContext.ParseResult.ValueForOption(InteractiveOption);
            AdditionalSources = invocationContext.ParseResult.ValueForOption(AddSourceOption);
        }

        public IReadOnlyList<string> TemplatePackages { get; }

        public bool Interactive { get; }

        public IReadOnlyList<string>? AdditionalSources { get; }

        private static Argument<IReadOnlyList<string>> NameArgument { get; } = new("name")
        {
            Description = "Name of NuGet package or folder.",
            Arity = new ArgumentArity(1, 99),
            IsHidden = true
        };

#pragma warning disable SA1202 // Elements should be ordered by access
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

        internal static void AddOptionsToCommand(Command command)
        {
            command.AddOption(InteractiveOption);
            command.AddOption(AddSourceOption);
        }

        internal static void AddToCommand(Command command)
        {
            command.AddArgument(NameArgument);
            AddOptionsToCommand(command);
        }
    }

    internal class InstallCommandArgs : GlobalArgs
    {
        public InstallCommandArgs(InvocationContext invocationContext)
            : base(invocationContext)
        {
            TemplatePackages = invocationContext.ParseResult.ValueForArgument(NameArgument) ?? throw new Exception("This shouldn't happen, we set ArgumentArity(1)...");
            Interactive = invocationContext.ParseResult.ValueForOption(InteractiveOption);
            AdditionalSources = invocationContext.ParseResult.ValueForOption(AddSourceOption);
        }

        public IReadOnlyList<string> TemplatePackages { get; }

        public bool Interactive { get; }

        public IReadOnlyList<string>? AdditionalSources { get; }

        private static Argument<IReadOnlyList<string>> NameArgument { get; } = new("name")
        {
            Description = "Name of NuGet package or folder.",
            Arity = new ArgumentArity(1, 99)
        };

#pragma warning disable SA1202 // Elements should be ordered by access
        internal static Option<bool> InteractiveOption { get; } = new("--interactive")
        {
            Description = "When downloading enable NuGet interactive."
        };

        internal static Option<IReadOnlyList<string>> AddSourceOption { get; } = new(new[] { "--add-source" })
        {
            Description = "Add NuGet source when looking for package.",
            AllowMultipleArgumentsPerToken = true,
        };

        internal static void AddToCommand(Command command)
        {
            command.AddArgument(NameArgument);
            command.AddOption(InteractiveOption);
            command.AddOption(AddSourceOption);
        }
#pragma warning restore SA1202 // Elements should be ordered by access
    }
}
