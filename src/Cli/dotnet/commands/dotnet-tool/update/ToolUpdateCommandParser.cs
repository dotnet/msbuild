// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools.Tool.Common;
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
                    $"-g|--{ToolAppliedOption.GlobalOption}",
                    LocalizableStrings.GlobalOptionDescription,
                    Accept.NoArguments()),
                Create.Option(
                    $"--{ToolAppliedOption.ToolPathOption}",
                    LocalizableStrings.ToolPathOptionDescription,
                    Accept.ExactlyOneArgument()
                          .With(name: LocalizableStrings.ToolPathOptionName)),
                Create.Option(
                    $"--{ToolAppliedOption.LocalOption}",
                    LocalizableStrings.LocalOptionDescription,
                    Accept.NoArguments()),
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
                Create.Option(
                    "--version",
                    LocalizableStrings.VersionOptionDescription,
                    Accept.ExactlyOneArgument()
                        .With(name: LocalizableStrings.VersionOptionName)),
                Create.Option(
                    $"--{ToolAppliedOption.ToolManifest}",
                    LocalizableStrings.ManifestPathOptionDescription,
                    Accept.ZeroOrOneArgument()
                        .With(name: LocalizableStrings.ManifestPathOptionName)),
                ToolCommandRestorePassThroughOptions.DisableParallelOption(),
                ToolCommandRestorePassThroughOptions.IgnoreFailedSourcesOption(),
                ToolCommandRestorePassThroughOptions.NoCacheOption(),
                ToolCommandRestorePassThroughOptions.InteractiveRestoreOption(),
                CommonOptions.HelpOption(),
                CommonOptions.VerbosityOption());
        }
    }
}
