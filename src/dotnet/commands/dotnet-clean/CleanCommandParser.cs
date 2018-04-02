// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Clean.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class CleanCommandParser
    {
        public static Command Clean() =>
            Create.Command(
                "clean",
                LocalizableStrings.AppFullName,
                Accept.ZeroOrMoreArguments(),
                CommonOptions.HelpOption(),
                Create.Option("-o|--output", 
                              LocalizableStrings.CmdOutputDirDescription,
                                         Accept.ExactlyOneArgument()
                        .With(name: LocalizableStrings.CmdOutputDir)
                        .ForwardAsSingle(o => $"-property:OutputPath={o.Arguments.Single()}")),
                CommonOptions.FrameworkOption(),
                CommonOptions.RuntimeOption(),
                CommonOptions.ConfigurationOption(),
                CommonOptions.VerbosityOption());
    }
}