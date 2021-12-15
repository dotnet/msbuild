// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Tool.Common;
using Microsoft.DotNet.Tools.Tool.Install;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Install.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolInstallCommandParser
    {
        public static readonly Argument<string> PackageIdArgument = new Argument<string>(LocalizableStrings.PackageIdArgumentName)
        {
            Description = LocalizableStrings.PackageIdArgumentDescription
        };

        public static readonly Option<string> VersionOption = new Option<string>("--version", LocalizableStrings.VersionOptionDescription)
        {
            ArgumentHelpName = LocalizableStrings.VersionOptionName
        };

        public static readonly Option<string> ConfigOption = new Option<string>("--configfile", LocalizableStrings.ConfigFileOptionDescription)
        {
            ArgumentHelpName = LocalizableStrings.ConfigFileOptionName
        };

        public static readonly Option<string[]> AddSourceOption = new Option<string[]>("--add-source", LocalizableStrings.AddSourceOptionDescription)
        {
            ArgumentHelpName = LocalizableStrings.AddSourceOptionName
        }.AllowSingleArgPerToken();

        public static readonly Option<string> FrameworkOption = new Option<string>("--framework", LocalizableStrings.FrameworkOptionDescription)
        {
            ArgumentHelpName = LocalizableStrings.FrameworkOptionName
        };

        public static readonly Option<bool> PrereleaseOption = ToolSearchCommandParser.PrereleaseOption;

        public static readonly Option<VerbosityOptions> VerbosityOption = CommonOptions.VerbosityOption;

        // Don't use the common options version as we don't want this to be a forwarded option
        public static readonly Option<string> ArchitectureOption = new Option<string>(new string[] { "--arch", "-a" }, CommonLocalizableStrings.ArchitectureOptionDescription);

        public static readonly Option<bool> GlobalOption = ToolAppliedOption.GlobalOption;
        
        public static readonly Option<bool> LocalOption = ToolAppliedOption.LocalOption;

        public static readonly Option<string> ToolPathOption = ToolAppliedOption.ToolPathOption;
        
        public static readonly Option<string> ToolManifestOption = ToolAppliedOption.ToolManifestOption;

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new Command("install", LocalizableStrings.CommandDescription);

            command.AddArgument(PackageIdArgument);
            command.AddOption(GlobalOption.WithHelpDescription(command, LocalizableStrings.GlobalOptionDescription));
            command.AddOption(LocalOption.WithHelpDescription(command, LocalizableStrings.LocalOptionDescription));
            command.AddOption(ToolPathOption.WithHelpDescription(command, LocalizableStrings.ToolPathOptionDescription));
            command.AddOption(VersionOption);
            command.AddOption(ConfigOption);
            command.AddOption(ToolManifestOption.WithHelpDescription(command, LocalizableStrings.ManifestPathOptionDescription));
            command.AddOption(AddSourceOption);
            command.AddOption(FrameworkOption);
            command.AddOption(PrereleaseOption);
            command.AddOption(ToolCommandRestorePassThroughOptions.DisableParallelOption);
            command.AddOption(ToolCommandRestorePassThroughOptions.IgnoreFailedSourcesOption);
            command.AddOption(ToolCommandRestorePassThroughOptions.NoCacheOption);
            command.AddOption(ToolCommandRestorePassThroughOptions.InteractiveRestoreOption);
            command.AddOption(VerbosityOption);
            command.AddOption(ArchitectureOption);

            command.SetHandler((parseResult) => new ToolInstallCommand(parseResult).Execute());

            return command;
        }
    }
}
