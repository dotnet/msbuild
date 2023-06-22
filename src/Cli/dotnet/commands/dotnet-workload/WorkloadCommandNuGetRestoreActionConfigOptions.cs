// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Restore.LocalizableStrings;
using Command = System.CommandLine.Command;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadCommandNuGetRestoreActionConfigOptions
    {
        public static Option<bool> DisableParallelOption = new ForwardedOption<bool>(
            "--disable-parallel",
            LocalizableStrings.CmdDisableParallelOptionDescription);

        public static Option<bool> NoCacheOption = new ForwardedOption<bool>(
            "--no-cache",
            LocalizableStrings.CmdNoCacheOptionDescription);

        public static Option<bool> IgnoreFailedSourcesOption = new ForwardedOption<bool>(
            "--ignore-failed-sources",
            LocalizableStrings.CmdIgnoreFailedSourcesOptionDescription);

        public static Option<bool> InteractiveRestoreOption = new ForwardedOption<bool>(
            "--interactive",
            CommonLocalizableStrings.CommandInteractiveOptionDescription);

        public static Option<bool> HiddenDisableParallelOption = new ForwardedOption<bool>(
            "--disable-parallel",
            LocalizableStrings.CmdDisableParallelOptionDescription).Hide();

        public static Option<bool> HiddenNoCacheOption = new ForwardedOption<bool>(
            "--no-cache",
            LocalizableStrings.CmdNoCacheOptionDescription).Hide();

        public static Option<bool> HiddenIgnoreFailedSourcesOption = new ForwardedOption<bool>(
            "--ignore-failed-sources",
            LocalizableStrings.CmdIgnoreFailedSourcesOptionDescription).Hide();

        public static Option<bool> HiddenInteractiveRestoreOption = new ForwardedOption<bool>(
            "--interactive",
            CommonLocalizableStrings.CommandInteractiveOptionDescription).Hide();

        public static RestoreActionConfig ToRestoreActionConfig(this ParseResult parseResult)
        {
            return new RestoreActionConfig(DisableParallel: parseResult.GetValue(DisableParallelOption),
                NoCache: parseResult.GetValue(NoCacheOption),
                IgnoreFailedSources: parseResult.GetValue(IgnoreFailedSourcesOption),
                Interactive: parseResult.GetValue(InteractiveRestoreOption));
        }

        public static void AddWorkloadCommandNuGetRestoreActionConfigOptions(this Command command, bool Hide = false)
        {
            command.AddOption(Hide ? HiddenDisableParallelOption : DisableParallelOption);
            command.AddOption(Hide ? HiddenIgnoreFailedSourcesOption : IgnoreFailedSourcesOption);
            command.AddOption(Hide ? HiddenNoCacheOption : NoCacheOption);
            command.AddOption(Hide ? HiddenInteractiveRestoreOption : InteractiveRestoreOption);
        }
    }
}
