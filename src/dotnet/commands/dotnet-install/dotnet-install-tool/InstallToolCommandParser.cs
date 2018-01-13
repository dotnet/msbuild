// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Install.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class InstallToolCommandParser
    {
        public static Command InstallTool()
        {
            return Create.Command("tool",
                LocalizableStrings.InstallToolCommandDefinition,
                Accept.ExactlyOneArgument(o => "packageId")
                    .With(name: "packageId",
                        description: LocalizableStrings.InstallToolPackageIdDefinition),
                Create.Option(
                    "--version",
                    LocalizableStrings.InstallToolVersionDefinition,
                    Accept.ExactlyOneArgument()),
                Create.Option(
                    "--configfile",
                    LocalizableStrings.InstallToolConfigfileDefinition,
                    Accept.ExactlyOneArgument()),
                Create.Option(
                    "--source",
                    LocalizableStrings.SourceOptionDescription,
                    Accept.ExactlyOneArgument()
                        .With(name: LocalizableStrings.SourceOptionName)),
                Create.Option(
                    "-f|--framework",
                    LocalizableStrings.InstallToolFrameworkDefinition,
                    Accept.ExactlyOneArgument()),
                CommonOptions.HelpOption());
        }
    }
}
