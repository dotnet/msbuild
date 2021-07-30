// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Tool.Common;
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

        public static readonly Option<VerbosityOptions> VerbosityOption = CommonOptions.VerbosityOption();

        // Don't use the common options version as we don't want this to be a forwarded option
        public static readonly Option<string> ArchitectureOption = new Option<string>(new string[] { "--arch", "-a" }, CommonLocalizableStrings.ArchitectureOptionDescription);

        public static Command GetCommand()
        {
            var command = new Command("install", LocalizableStrings.CommandDescription);

            command.AddArgument(PackageIdArgument);
            command.AddOption(ToolAppliedOption.GlobalOption(LocalizableStrings.GlobalOptionDescription));
            command.AddOption(ToolAppliedOption.LocalOption(LocalizableStrings.LocalOptionDescription));
            command.AddOption(ToolAppliedOption.ToolPathOption(LocalizableStrings.ToolPathOptionDescription, LocalizableStrings.ToolPathOptionName));
            command.AddOption(VersionOption);
            command.AddOption(ConfigOption);
            command.AddOption(ToolAppliedOption.ToolManifestOption(LocalizableStrings.ManifestPathOptionDescription, LocalizableStrings.ManifestPathOptionName));
            command.AddOption(AddSourceOption);
            command.AddOption(FrameworkOption);
            command.AddOption(ToolCommandRestorePassThroughOptions.DisableParallelOption);
            command.AddOption(ToolCommandRestorePassThroughOptions.IgnoreFailedSourcesOption);
            command.AddOption(ToolCommandRestorePassThroughOptions.NoCacheOption);
            command.AddOption(ToolCommandRestorePassThroughOptions.InteractiveRestoreOption);
            command.AddOption(VerbosityOption);
            command.AddOption(ArchitectureOption);

            return command;
        }
    }
}
