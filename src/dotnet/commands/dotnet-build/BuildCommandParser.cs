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
                      .With(name: CommonLocalizableStrings.CmdProjectFile,
                            description:
                            "The MSBuild project file to build. If a project file is not specified, MSBuild searches the current working directory for a file that has a file extension that ends in `proj` and uses that file."),
                CommonOptions.HelpOption(),
                Create.Option(
                    "-o|--output",
                    LocalizableStrings.OutputOptionDescription,
                    Accept.ExactlyOneArgument()
                          .With(name: LocalizableStrings.OutputOptionName)
                          .ForwardAsSingle(o => $"-property:OutputPath={o.Arguments.Single()}")),
                CommonOptions.FrameworkOption(),
                CommonOptions.RuntimeOption(),
                CommonOptions.ConfigurationOption(),
                CommonOptions.VersionSuffixOption(),
                Create.Option(
                    "--no-incremental",
                    LocalizableStrings.NoIncrementialOptionDescription),
                Create.Option(
                    "--no-dependencies",
                    LocalizableStrings.NoDependenciesOptionDescription,
                    Accept.NoArguments()
                          .ForwardAs("-property:BuildProjectReferences=false")),
                CommonOptions.NoRestoreOption(),
                CommonOptions.VerbosityOption());
    }
}