// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Restore.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolRestoreCommandParser
    {
        public static Command ToolRestore()
        {
            return Create.Command(
                "restore",
                LocalizableStrings.CommandDescription,
                Accept.NoArguments(),
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
                    "--tool-manifest",
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
