// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Clean.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class CleanCommandParser
    {
        public static Command Clean() =>
            Create.Command(
                "clean",
                LocalizableStrings.AppFullName,
                Accept.ZeroOrMoreArguments()
                      .With(name: CommonLocalizableStrings.SolutionOrProjectArgumentName,
                            description: CommonLocalizableStrings.SolutionOrProjectArgumentDescription),
                CommonOptions.HelpOption(),
                Create.Option("-o|--output", 
                              LocalizableStrings.CmdOutputDirDescription,
                                         Accept.ExactlyOneArgument()
                        .With(name: LocalizableStrings.CmdOutputDir)
                        .ForwardAsSingle(o => $"-property:OutputPath={CommandDirectoryContext.GetFullPath(o.Arguments.Single())}")),
                Create.Option(
                    "--nologo|/nologo",
                    LocalizableStrings.CmdNoLogo,
                    Accept.NoArguments()
                          .ForwardAs("-nologo")),
                CommonOptions.FrameworkOption(LocalizableStrings.FrameworkOptionDescription),
                CommonOptions.RuntimeOption(LocalizableStrings.RuntimeOptionDescription),
                CommonOptions.ConfigurationOption(LocalizableStrings.ConfigurationOptionDescription),
                CommonOptions.InteractiveMsBuildForwardOption(),
                CommonOptions.VerbosityOption());
    }
}
