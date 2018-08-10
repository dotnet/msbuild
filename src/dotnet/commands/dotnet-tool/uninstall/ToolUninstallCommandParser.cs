// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Uninstall.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolUninstallCommandParser
    {
        public static Command ToolUninstall()
        {
            return Create.Command("uninstall",
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
                CommonOptions.HelpOption());
        }
    }
}
