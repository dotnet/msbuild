// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Tools.Tool.Common;
using Microsoft.DotNet.Tools.Tool.Restore;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Restore.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolRestoreCommandParser
    {
        public static readonly Option<string> ConfigOption = ToolInstallCommandParser.ConfigOption;

        public static readonly Option<string[]> AddSourceOption = ToolInstallCommandParser.AddSourceOption;

        public static readonly Option<string> ToolManifestOption = ToolAppliedOption.ToolManifestOption;

        public static readonly Option<VerbosityOptions> VerbosityOption = ToolInstallCommandParser.VerbosityOption;

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new Command("restore", LocalizableStrings.CommandDescription);

            command.AddOption(ConfigOption);
            command.AddOption(AddSourceOption);
            command.AddOption(ToolManifestOption.WithHelpDescription(command, LocalizableStrings.ManifestPathOptionDescription));
            command.AddOption(ToolCommandRestorePassThroughOptions.DisableParallelOption);
            command.AddOption(ToolCommandRestorePassThroughOptions.IgnoreFailedSourcesOption);
            command.AddOption(ToolCommandRestorePassThroughOptions.NoCacheOption);
            command.AddOption(ToolCommandRestorePassThroughOptions.InteractiveRestoreOption);
            command.AddOption(VerbosityOption);

            command.SetHandler((parseResult) => new ToolRestoreCommand(parseResult).Execute());

            return command;
        }
    }
}
