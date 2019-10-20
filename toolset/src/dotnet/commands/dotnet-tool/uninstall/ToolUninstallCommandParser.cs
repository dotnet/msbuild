// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools.Tool.Common;
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
                    $"-g|--{ToolAppliedOption.GlobalOption}",
                    LocalizableStrings.GlobalOptionDescription,
                    Accept.NoArguments()),
                Create.Option(
                    $"--{ToolAppliedOption.LocalOption}",
                    LocalizableStrings.LocalOptionDescription,
                    Accept.NoArguments()),
                Create.Option(
                    $"--{ToolAppliedOption.ToolPathOption}",
                    LocalizableStrings.ToolPathOptionDescription,
                    Accept.ExactlyOneArgument()
                          .With(name: LocalizableStrings.ToolPathOptionName)),
                Create.Option(
                    $"--{ToolAppliedOption.ToolManifest}",
                    LocalizableStrings.ManifestPathOptionDescription,
                    Accept.ZeroOrOneArgument()
                        .With(name: LocalizableStrings.ManifestPathOptionName)),
                CommonOptions.HelpOption());
        }
    }
}
