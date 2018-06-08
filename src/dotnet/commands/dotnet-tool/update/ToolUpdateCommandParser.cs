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
                    LocalizableStrings.ToolPathOptionDescription,
                    Accept.ExactlyOneArgument()
                          .With(name: LocalizableStrings.ToolPathOptionName)),
                Create.Option(
                    "--configfile",
                    LocalizableStrings.ConfigFileOptionDescription,
                    Accept.ExactlyOneArgument()
                          .With(name: LocalizableStrings.ConfigFileOptionName)),
                Create.Option(
                    "--add-source",
                    LocalizableStrings.AddSourceOptionDescription,
                    Accept.OneOrMoreArguments()
                        .With(name: LocalizableStrings.AddSourceOptionName)),
                Create.Option(
                    "--framework",
                    LocalizableStrings.FrameworkOptionDescription,
                    Accept.ExactlyOneArgument()
                          .With(name: LocalizableStrings.FrameworkOptionName)),
                CommonOptions.HelpOption(),
                CommonOptions.VerbosityOption());
        }
    }
}
