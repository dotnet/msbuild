// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Cli.Extensions;
using Microsoft.TemplateEngine.Cli.HelpAndUsage;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;

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

    internal class InstallCommandArgs : GlobalArgs
    {
        public InstallCommandArgs(InvocationContext invocationContext)
            : base(invocationContext)
        {
            TemplatePackages = invocationContext.ParseResult.ValueForArgument(NameArgument) ?? throw new Exception("This shouldn't happen, we set ArgumentArity(1)...");
            Interactive = invocationContext.ParseResult.ValueForOption(InteractiveOption);
            AdditionalSources = invocationContext.ParseResult.ValueForOption(AddSourceOption);
        }

        [Obsolete]
        internal InstallCommandArgs(INewCommandInput legacyArgs) : base(legacyArgs)
        {
            TemplatePackages = legacyArgs.ToInstallList ?? throw new ArgumentNullException(nameof(legacyArgs.ToInstallList));
            AdditionalSources = legacyArgs.InstallNuGetSourceList;
            Interactive = legacyArgs.IsInteractiveFlagSpecified;
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

        private static Option<IReadOnlyList<string>> AddSourceOption { get; } = new(new[] { "--add-source", "--nuget-source" })
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
    }
}
