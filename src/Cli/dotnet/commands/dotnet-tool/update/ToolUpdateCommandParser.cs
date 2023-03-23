// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Tools.Tool.Common;
using Microsoft.DotNet.Tools.Tool.Update;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Update.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolUpdateCommandParser
    {
        public static readonly Argument<string> PackageIdArgument = ToolInstallCommandParser.PackageIdArgument;

        public static readonly Option<bool> GlobalOption = ToolAppliedOption.GlobalOption;

        public static readonly Option<string> ToolPathOption = ToolAppliedOption.ToolPathOption;

        public static readonly Option<bool> LocalOption = ToolAppliedOption.LocalOption;

        public static readonly Option<string> ConfigOption = ToolInstallCommandParser.ConfigOption;

        public static readonly Option<string[]> AddSourceOption = ToolInstallCommandParser.AddSourceOption;

        public static readonly Option<string> FrameworkOption = ToolInstallCommandParser.FrameworkOption;

        public static readonly Option<string> VersionOption = ToolInstallCommandParser.VersionOption;

        public static readonly Option<string> ToolManifestOption = ToolAppliedOption.ToolManifestOption;

        public static readonly Option<bool> PrereleaseOption = ToolSearchCommandParser.PrereleaseOption;

        public static readonly Option<VerbosityOptions> VerbosityOption = ToolInstallCommandParser.VerbosityOption;

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new Command("update", LocalizableStrings.CommandDescription);

            command.AddArgument(PackageIdArgument);
            command.AddOption(GlobalOption.WithHelpDescription(command, LocalizableStrings.GlobalOptionDescription));
            command.AddOption(ToolPathOption.WithHelpDescription(command, LocalizableStrings.ToolPathOptionDescription));
            command.AddOption(LocalOption.WithHelpDescription(command, LocalizableStrings.LocalOptionDescription));
            command.AddOption(ConfigOption);
            command.AddOption(AddSourceOption);
            command.AddOption(FrameworkOption);
            command.AddOption(VersionOption);
            command.AddOption(ToolManifestOption.WithHelpDescription(command, LocalizableStrings.ManifestPathOptionDescription));
            command.AddOption(PrereleaseOption);
            command.AddOption(ToolCommandRestorePassThroughOptions.DisableParallelOption);
            command.AddOption(ToolCommandRestorePassThroughOptions.IgnoreFailedSourcesOption);
            command.AddOption(ToolCommandRestorePassThroughOptions.NoCacheOption);
            command.AddOption(ToolCommandRestorePassThroughOptions.InteractiveRestoreOption);
            command.AddOption(VerbosityOption);

            command.SetHandler((parseResult) => new ToolUpdateCommand(parseResult).Execute());

            return command;
        }
    }
}
