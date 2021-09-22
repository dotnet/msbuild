// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

        public static readonly Option<bool> GlobalOption = ToolAppliedOption.GlobalOption(LocalizableStrings.GlobalOptionDescription);

        public static readonly Option<bool> LocalOption = ToolAppliedOption.LocalOption(LocalizableStrings.LocalOptionDescription);

        public static readonly Option<string> ToolPathOption = ToolAppliedOption.ToolPathOption(LocalizableStrings.ToolPathOptionDescription, LocalizableStrings.ToolPathOptionName);

        public static readonly Option<string> ToolManifestOption = ToolAppliedOption.ToolManifestOption(LocalizableStrings.ManifestPathOptionDescription, LocalizableStrings.ManifestPathOptionName);

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new Command("uninstall", LocalizableStrings.CommandDescription);

            command.AddArgument(PackageIdArgument);
            command.AddOption(GlobalOption);
            command.AddOption(LocalOption);
            command.AddOption(ToolPathOption);
            command.AddOption(ToolManifestOption);

            command.Handler = CommandHandler.Create<ParseResult>((parseResult) => new ToolUninstallCommand(parseResult).Execute());

            return command;
        }
    }
}
