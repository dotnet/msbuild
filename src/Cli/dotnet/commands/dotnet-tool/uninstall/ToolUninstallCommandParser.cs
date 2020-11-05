// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using Microsoft.DotNet.Tools.Tool.Common;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Uninstall.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolUninstallCommandParser
    {
        public static readonly Argument PackageIdArgument = ToolInstallCommandParser.PackageIdArgument;

        public static readonly Option GlobalOption = ToolAppliedOption.GlobalOption;

        public static readonly Option LocalOption = ToolAppliedOption.LocalOption;

        public static readonly Option ToolPathOption = ToolAppliedOption.ToolPathOption;

        public static readonly Option ToolManifestOption = ToolAppliedOption.ToolManifestOption;

        public static Command GetCommand()
        {
            var command = new Command("uninstall", LocalizableStrings.CommandDescription);

            command.AddArgument(PackageIdArgument);
            command.AddOption(GlobalOption);
            command.AddOption(LocalOption);
            command.AddOption(ToolPathOption);
            command.AddOption(ToolManifestOption);
            command.AddOption(CommonOptions.DiagOption());

            return command;
        }
    }
}
