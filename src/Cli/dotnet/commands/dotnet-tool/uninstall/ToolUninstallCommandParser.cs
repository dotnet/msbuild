// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Tools.Tool.Common;
using Microsoft.DotNet.Tools.Tool.Search;
using Microsoft.DotNet.Tools.Tool.Uninstall;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Uninstall.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolUninstallCommandParser
    {
        public static readonly Argument<string> PackageIdArgument = ToolInstallCommandParser.PackageIdArgument;

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
            var command = new Command("uninstall", LocalizableStrings.CommandDescription);

            command.AddArgument(PackageIdArgument);
            command.AddOption(GlobalOption.WithHelpDescription(command, LocalizableStrings.GlobalOptionDescription));
            command.AddOption(LocalOption.WithHelpDescription(command, LocalizableStrings.LocalOptionDescription));
            command.AddOption(ToolPathOption.WithHelpDescription(command, LocalizableStrings.ToolPathOptionDescription));
            command.AddOption(ToolManifestOption.WithHelpDescription(command, LocalizableStrings.ManifestPathOptionDescription));

            command.SetHandler((parseResult) => new ToolUninstallCommand(parseResult).Execute());

            return command;
        }
    }
}
