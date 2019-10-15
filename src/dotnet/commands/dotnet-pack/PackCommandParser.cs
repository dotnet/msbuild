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
                Accept.ZeroOrMoreArguments()
                      .With(name: CommonLocalizableStrings.SolutionOrProjectArgumentName,
                            description: CommonLocalizableStrings.SolutionOrProjectArgumentDescription),
                CommonOptions.HelpOption(),
                Create.Option(
                    "-o|--output",
                    LocalizableStrings.CmdOutputDirDescription,
                    Accept.ExactlyOneArgument()
                        .With(name: LocalizableStrings.CmdOutputDir)
                        .ForwardAsSingle(o => $"-property:PackageOutputPath={CommandDirectoryContext.GetFullPath(o.Arguments.Single())}")),
                Create.Option(
                    "--no-build",
                    LocalizableStrings.CmdNoBuildOptionDescription,
                    Accept.NoArguments().ForwardAs("-property:NoBuild=true")),
                Create.Option(
                    "--include-symbols",
                    LocalizableStrings.CmdIncludeSymbolsDescription,
                    Accept.NoArguments().ForwardAs("-property:IncludeSymbols=true")),
                Create.Option(
                    "--include-source",
                    LocalizableStrings.CmdIncludeSourceDescription,
                    Accept.NoArguments().ForwardAs("-property:IncludeSource=true")),
                CommonOptions.ConfigurationOption(LocalizableStrings.ConfigurationOptionDescription),
                CommonOptions.VersionSuffixOption(),
                Create.Option(
                    "-s|--serviceable",
                    LocalizableStrings.CmdServiceableDescription,
                    Accept.NoArguments().ForwardAs("-property:Serviceable=true")),
                Create.Option(
                    "--nologo|/nologo",
                    LocalizableStrings.CmdNoLogo,
                    Accept.NoArguments()
                          .ForwardAs("-nologo")),
                CommonOptions.InteractiveMsBuildForwardOption(),
                CommonOptions.NoRestoreOption(),
                CommonOptions.VerbosityOption());
    }
}
