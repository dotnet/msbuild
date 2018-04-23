// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Update.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolUpdateCommandParser
    {
        public static Command ToolUpdate()
        {
            return Create.Command("update",
                LocalizableStrings.CommandDescription,
                Accept.ExactlyOneArgument(errorMessage: o => LocalizableStrings.SpecifyExactlyOnePackageId)
                    .With(name: LocalizableStrings.PackageIdArgumentName,
                        description: LocalizableStrings.PackageIdArgumentDescription),
                Create.Option(
                    "-g|--global",
                    LocalizableStrings.GlobalOptionDescription,
                    Accept.NoArguments()),
                Create.Option(
                    "--tool-path",
                    LocalizableStrings.ToolPathDescription,
                    Accept.ExactlyOneArgument()),
                Create.Option(
                    "--configfile",
                    LocalizableStrings.ConfigFileOptionDescription,
                    Accept.ExactlyOneArgument()),
                Create.Option(
                    "--source-feed",
                    LocalizableStrings.SourceFeedOptionDescription,
                    Accept.OneOrMoreArguments()
                        .With(name: LocalizableStrings.SourceFeedOptionName)),
                Create.Option(
                    "--framework",
                    LocalizableStrings.FrameworkOptionDescription,
                    Accept.ExactlyOneArgument()),
                CommonOptions.HelpOption(),
                CommonOptions.VerbosityOption());
        }
    }
}
