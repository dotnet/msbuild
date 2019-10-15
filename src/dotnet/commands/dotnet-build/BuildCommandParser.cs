// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Build.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class BuildCommandParser
    {
        public static Command Build() =>
            CreateWithRestoreOptions.Command(
                "build",
                LocalizableStrings.AppFullName,
                Accept.ZeroOrMoreArguments()
                      .With(name: CommonLocalizableStrings.SolutionOrProjectArgumentName,
                            description: CommonLocalizableStrings.SolutionOrProjectArgumentDescription),
                CommonOptions.HelpOption(),
                Create.Option(
                    "-o|--output",
                    LocalizableStrings.OutputOptionDescription,
                    Accept.ExactlyOneArgument()
                          .With(name: LocalizableStrings.OutputOptionName)
                          .ForwardAsSingle(o => $"-property:OutputPath={CommandDirectoryContext.GetFullPath(o.Arguments.Single())}")),
                CommonOptions.FrameworkOption(LocalizableStrings.FrameworkOptionDescription),
                CommonOptions.ConfigurationOption(LocalizableStrings.ConfigurationOptionDescription),
                CommonOptions.RuntimeOption(LocalizableStrings.RuntimeOptionDescription),
                CommonOptions.VersionSuffixOption(),
                Create.Option(
                    "--no-incremental",
                    LocalizableStrings.NoIncrementalOptionDescription),
                Create.Option(
                    "--no-dependencies",
                    LocalizableStrings.NoDependenciesOptionDescription,
                    Accept.NoArguments()
                          .ForwardAs("-property:BuildProjectReferences=false")),
                Create.Option(
                    "--nologo|/nologo",
                    LocalizableStrings.CmdNoLogo,
                    Accept.NoArguments()
                          .ForwardAs("-nologo")),
                CommonOptions.NoRestoreOption(),
                CommonOptions.InteractiveMsBuildForwardOption(),
                CommonOptions.VerbosityOption());
    }
}
