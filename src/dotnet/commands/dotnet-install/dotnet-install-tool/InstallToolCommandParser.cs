// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Install.Tool.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class InstallToolCommandParser
    {
        public static Command InstallTool()
        {
            return Create.Command("tool",
                LocalizableStrings.CommandDescription,
                Accept.ExactlyOneArgument(o => "packageId")
                    .With(name: LocalizableStrings.PackageIdArgumentName,
                          description: LocalizableStrings.PackageIdArgumentDescription),
                Create.Option(
                    "--version",
                    LocalizableStrings.VersionOptionDescription,
                    Accept.ExactlyOneArgument()),
                Create.Option(
                    "--configfile",
                    LocalizableStrings.ConfigFileOptionDescription,
                    Accept.ExactlyOneArgument()),
                Create.Option(
                    "--source",
                    LocalizableStrings.SourceOptionDescription,
                    Accept.ExactlyOneArgument()
                        .With(name: LocalizableStrings.SourceOptionName)),
                Create.Option(
                    "-f|--framework",
                    LocalizableStrings.FrameworkOptionDescription,
                    Accept.ExactlyOneArgument()),
                CommonOptions.HelpOption());
        }
    }
}
