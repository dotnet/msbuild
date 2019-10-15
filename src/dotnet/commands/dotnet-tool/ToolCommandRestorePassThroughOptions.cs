// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Restore.LocalizableStrings;


namespace Microsoft.DotNet.Cli
{
    internal static class ToolCommandRestorePassThroughOptions
    {
        public static Option DisableParallelOption()
        {
            return Create.Option(
                "--disable-parallel",
                LocalizableStrings.CmdDisableParallelOptionDescription,
                Accept.NoArguments().ForwardAs("--disable-parallel"));
        }

        public static Option NoCacheOption()
        {
            return Create.Option(
                "--no-cache",
                LocalizableStrings.CmdNoCacheOptionDescription,
                Accept.NoArguments().ForwardAs("--no-cache"));
        }

        public static Option IgnoreFailedSourcesOption()
        {
            return Create.Option(
                "--ignore-failed-sources",
                LocalizableStrings.CmdIgnoreFailedSourcesOptionDescription,
                Accept.NoArguments().ForwardAs("--ignore-failed-sources"));
        }

        public static Option InteractiveRestoreOption()
        {
            return Create.Option(
                        "--interactive",
                        CommonLocalizableStrings.CommandInteractiveOptionDescription,
                        Accept.NoArguments()
                            .ForwardAs(Constants.RestoreInteractiveOption));
        }
    }
}
