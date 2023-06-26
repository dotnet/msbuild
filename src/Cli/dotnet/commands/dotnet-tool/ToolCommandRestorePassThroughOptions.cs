// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Restore.LocalizableStrings;


namespace Microsoft.DotNet.Cli
{
    internal static class ToolCommandRestorePassThroughOptions
    {
        public static Option<bool> DisableParallelOption = new ForwardedOption<bool>(
                "--disable-parallel",
                LocalizableStrings.CmdDisableParallelOptionDescription)
                .ForwardAs("--disable-parallel");

        public static Option<bool> NoCacheOption = new ForwardedOption<bool>(
                "--no-cache",
                LocalizableStrings.CmdNoCacheOptionDescription)
                .ForwardAs("--no-cache");

        public static Option<bool> IgnoreFailedSourcesOption = new ForwardedOption<bool>(
                "--ignore-failed-sources",
                LocalizableStrings.CmdIgnoreFailedSourcesOptionDescription)
                .ForwardAs("--ignore-failed-sources");

        public static Option<bool> InteractiveRestoreOption = new ForwardedOption<bool>(
                "--interactive",
                CommonLocalizableStrings.CommandInteractiveOptionDescription)
                .ForwardAs(Constants.RestoreInteractiveOption);
    }
}
