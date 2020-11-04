// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Restore.LocalizableStrings;


namespace Microsoft.DotNet.Cli
{
    internal static class ToolCommandRestorePassThroughOptions
    {
        public static Option DisableParallelOption = new Option<bool>(
                "--disable-parallel",
                LocalizableStrings.CmdDisableParallelOptionDescription)
                .ForwardAs("--disable-parallel");

        public static Option NoCacheOption = new Option<bool>(
                "--no-cache",
                LocalizableStrings.CmdNoCacheOptionDescription)
                .ForwardAs("--no-cache");

        public static Option IgnoreFailedSourcesOption = new Option<bool>(
                "--ignore-failed-sources",
                LocalizableStrings.CmdIgnoreFailedSourcesOptionDescription)
                .ForwardAs("--ignore-failed-sources");

        public static Option InteractiveRestoreOption = new Option<bool>(
                "--interactive",
                CommonLocalizableStrings.CommandInteractiveOptionDescription)
                .ForwardAs(Constants.RestoreInteractiveOption);
    }
}
