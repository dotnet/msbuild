// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Install.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolInstallCommandParser
    {
        public static Command ToolInstall()
        {
            return Create.Command("install",
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
                    "--version",
                    LocalizableStrings.VersionOptionDescription,
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
                    "-f|--framework",
                    LocalizableStrings.FrameworkOptionDescription,
                    Accept.ExactlyOneArgument()),
                CommonOptions.HelpOption(),
                CommonOptions.VerbosityOption());
        }
    }
}
