// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Pack.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class PackCommandParser
    {
        public static Command Pack() =>
            CreateWithRestoreOptions.Command(
                "pack",
                LocalizableStrings.AppFullName,
                Accept.ZeroOrMoreArguments(),
                CommonOptions.HelpOption(),
                Create.Option(
                    "-o|--output",
                    LocalizableStrings.CmdOutputDirDescription,
                    Accept.ExactlyOneArgument()
                        .With(name: LocalizableStrings.CmdOutputDir)
                        .ForwardAsSingle(o => $"-p:PackageOutputPath={o.Arguments.Single()}")),
                Create.Option(
                    "--no-build",
                    LocalizableStrings.CmdNoBuildOptionDescription,
                    Accept.NoArguments().ForwardAs("-p:NoBuild=true")),
                Create.Option(
                    "--include-symbols",
                    LocalizableStrings.CmdIncludeSymbolsDescription,
                    Accept.NoArguments().ForwardAs("-p:IncludeSymbols=true")),
                Create.Option(
                    "--include-source",
                    LocalizableStrings.CmdIncludeSourceDescription,
                    Accept.NoArguments().ForwardAs("-p:IncludeSource=true")),
                CommonOptions.ConfigurationOption(),
                CommonOptions.VersionSuffixOption(),
                Create.Option(
                    "-s|--serviceable",
                    LocalizableStrings.CmdServiceableDescription,
                    Accept.NoArguments().ForwardAs("-p:Serviceable=true")),
                CommonOptions.NoRestoreOption(),
                CommonOptions.VerbosityOption());
    }
}