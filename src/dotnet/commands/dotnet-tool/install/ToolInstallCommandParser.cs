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
                    LocalizableStrings.ToolPathOptionDescription,
                    Accept.ExactlyOneArgument()
                          .With(name: LocalizableStrings.ToolPathOptionName)),
                Create.Option(
                    "--version",
                    LocalizableStrings.VersionOptionDescription,
                    Accept.ExactlyOneArgument()
                          .With(name: LocalizableStrings.VersionOptionName)),
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
