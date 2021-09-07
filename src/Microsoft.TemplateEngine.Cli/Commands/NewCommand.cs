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
    internal class NewCommand : BaseCommand<NewCommandArgs>
    {
        private readonly string _commandName;

        internal NewCommand(string commandName, ITemplateEngineHost host, ITelemetryLogger logger, New3Callbacks callbacks) : base(host, logger, callbacks)
        {
            _commandName = commandName;
        }

        protected override Command CreateCommandAbstract()
        {
            Command command = new Command(_commandName, LocalizableStrings.CommandDescription);
            NewCommandArgs.AddToCommand(command);
            command.TreatUnmatchedTokensAsErrors = true;
            return command;
        }

        protected override Task<New3CommandStatus> ExecuteAsync(NewCommandArgs args, IEngineEnvironmentSettings environmentSettings, CancellationToken cancellationToken)
        {
            if (TemplatePackageCoordinator.IsTemplatePackageManipulationFlow(args))
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

#pragma warning disable CS0612 // Type or member is obsolete
                return templatePackageCoordinator.ProcessAsync(args, cancellationToken);
#pragma warning restore CS0612 // Type or member is obsolete
            }

            throw new NotImplementedException();
        }

        protected override NewCommandArgs ParseContext(InvocationContext context) => new(context);
    }

    internal partial class NewCommandArgs : GlobalArgs
    {
        public NewCommandArgs(InvocationContext invocationContext) : base(invocationContext)
        {
            InstallItems = invocationContext.ParseResult.ValueForOption(InstallOption);
            Interactive = invocationContext.ParseResult.ValueForOption(InteractiveOption);
            AdditionalNuGetSources = invocationContext.ParseResult.ValueForOption(AddSourceOption);
        }

        public IReadOnlyList<string>? InstallItems { get; }

        public bool Interactive { get; }

        public IReadOnlyList<string>? AdditionalNuGetSources { get; }

        private static Option<IReadOnlyList<string>> InstallOption { get; } = new(new[] { "-i", "--install" })
        {
            Description = LocalizableStrings.InstallHelp,
            AllowMultipleArgumentsPerToken = true,
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
            command.AddOption(InstallOption);
            command.AddOption(InteractiveOption);
            command.AddOption(AddSourceOption);
        }
    }
}
