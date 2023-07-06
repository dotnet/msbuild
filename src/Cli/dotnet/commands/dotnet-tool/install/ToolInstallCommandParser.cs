// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Tool.Common;
using Microsoft.DotNet.Tools.Tool.Install;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Install.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolInstallCommandParser
    {
        public static readonly CliArgument<string> PackageIdArgument = new("packageId")
        {
            HelpName = LocalizableStrings.PackageIdArgumentName,
            Description = LocalizableStrings.PackageIdArgumentDescription
        };

        public static readonly CliOption<string> VersionOption = new("--version")
        {
            Description = LocalizableStrings.VersionOptionDescription,
            HelpName = LocalizableStrings.VersionOptionName
        };

        public static readonly CliOption<string> ConfigOption = new("--configfile")
        {
            Description = LocalizableStrings.ConfigFileOptionDescription,
            HelpName = LocalizableStrings.ConfigFileOptionName
        };

        private static readonly CliOption<string[]> addSourceOption = new CliOption<string[]>("--add-source")
        {
            Description = LocalizableStrings.AddSourceOptionDescription,
            HelpName = LocalizableStrings.AddSourceOptionName
        }.AllowSingleArgPerToken();

        public static readonly CliOption<string> FrameworkOption = new("--framework")
        {
            Description = LocalizableStrings.FrameworkOptionDescription,
            HelpName = LocalizableStrings.FrameworkOptionName
        };

        public static readonly CliOption<bool> PrereleaseOption = ToolSearchCommandParser.PrereleaseOption;

        public static readonly CliOption<bool> CreateManifestIfNeededOption = new("--create-manifest-if-needed")
        {
            Description = LocalizableStrings.CreateManifestIfNeededOptionDescription
        };

        public static readonly CliOption<VerbosityOptions> VerbosityOption = CommonOptions.VerbosityOption;

        // Don't use the common options version as we don't want this to be a forwarded option
        public static readonly CliOption<string> ArchitectureOption = new("--arch", "-a")
        {
            Description = CommonLocalizableStrings.ArchitectureOptionDescription
        };

        public static readonly CliOption<bool> GlobalOption = ToolAppliedOption.GlobalOption;
        
        public static readonly CliOption<bool> LocalOption = ToolAppliedOption.LocalOption;

        public static readonly CliOption<string> ToolPathOption = ToolAppliedOption.ToolPathOption;
        
        public static readonly CliOption<string> ToolManifestOption = ToolAppliedOption.ToolManifestOption;

        private static readonly CliCommand Command = ConstructCommand();

        public static CliOption<string[]> AddSourceOption => addSourceOption;

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("install", LocalizableStrings.CommandDescription);

            command.Arguments.Add(PackageIdArgument);
            command.Options.Add(GlobalOption.WithHelpDescription(command, LocalizableStrings.GlobalOptionDescription));
            command.Options.Add(LocalOption.WithHelpDescription(command, LocalizableStrings.LocalOptionDescription));
            command.Options.Add(ToolPathOption.WithHelpDescription(command, LocalizableStrings.ToolPathOptionDescription));
            command.Options.Add(VersionOption);
            command.Options.Add(ConfigOption);
            command.Options.Add(ToolManifestOption.WithHelpDescription(command, LocalizableStrings.ManifestPathOptionDescription));
            command.Options.Add(AddSourceOption);
            command.Options.Add(FrameworkOption);
            command.Options.Add(PrereleaseOption);
            command.Options.Add(ToolCommandRestorePassThroughOptions.DisableParallelOption);
            command.Options.Add(ToolCommandRestorePassThroughOptions.IgnoreFailedSourcesOption);
            command.Options.Add(ToolCommandRestorePassThroughOptions.NoCacheOption);
            command.Options.Add(ToolCommandRestorePassThroughOptions.InteractiveRestoreOption);
            command.Options.Add(VerbosityOption);
            command.Options.Add(ArchitectureOption);
            command.Options.Add(CreateManifestIfNeededOption);

            command.SetAction((parseResult) => new ToolInstallCommand(parseResult).Execute());

            return command;
        }
    }
}
