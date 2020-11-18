// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using Microsoft.DotNet.Tools.Tool.Common;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Update.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolUpdateCommandParser
    {
        public static readonly Argument PackageIdArgument = ToolInstallCommandParser.PackageIdArgument;

        public static readonly Option GlobalOption = ToolAppliedOption.GlobalOption(LocalizableStrings.GlobalOptionDescription);

        public static readonly Option ToolPathOption = ToolAppliedOption.ToolPathOption(LocalizableStrings.ToolPathOptionDescription, LocalizableStrings.ToolPathOptionName);

        public static readonly Option LocalOption = ToolAppliedOption.LocalOption(LocalizableStrings.LocalOptionDescription);

        public static readonly Option ConfigOption = ToolInstallCommandParser.ConfigOption;

        public static readonly Option AddSourceOption = ToolInstallCommandParser.AddSourceOption;

        public static readonly Option FrameworkOption = ToolInstallCommandParser.FrameworkOption;

        public static readonly Option VersionOption = ToolInstallCommandParser.VersionOption;

        public static readonly Option ToolManifestOption = ToolAppliedOption.ToolManifestOption(LocalizableStrings.ManifestPathOptionDescription, LocalizableStrings.ManifestPathOptionName);

        public static readonly Option VerbosityOption = ToolInstallCommandParser.VerbosityOption;

        public static Command GetCommand()
        {
            var command = new Command("update", LocalizableStrings.CommandDescription);

            command.AddArgument(PackageIdArgument);
            command.AddOption(GlobalOption);
            command.AddOption(ToolPathOption);
            command.AddOption(LocalOption);
            command.AddOption(ConfigOption);
            command.AddOption(AddSourceOption);
            command.AddOption(FrameworkOption);
            command.AddOption(VersionOption);
            command.AddOption(ToolManifestOption);
            command.AddOption(ToolCommandRestorePassThroughOptions.DisableParallelOption);
            command.AddOption(ToolCommandRestorePassThroughOptions.IgnoreFailedSourcesOption);
            command.AddOption(ToolCommandRestorePassThroughOptions.NoCacheOption);
            command.AddOption(ToolCommandRestorePassThroughOptions.InteractiveRestoreOption);
            command.AddOption(VerbosityOption);

            return command;
        }
    }
}
