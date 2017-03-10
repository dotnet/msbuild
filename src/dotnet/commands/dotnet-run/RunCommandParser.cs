// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools.Run;
using LocalizableStrings = Microsoft.DotNet.Tools.Run.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class RunCommandParser
    {
        public static Command Run() =>
            Create.Command(
                "run",
                ".NET Run Command",
                Accept.ZeroOrMoreArguments
                .MaterializeAs(o =>
                {
                    return new RunCommand()
                    {
                        Configuration = o.ValueOrDefault<string>("--configuration"),
                        Framework = o.ValueOrDefault<string>("--framework"),
                        Project = o.ValueOrDefault<string>("--project"),
                        Args = (IReadOnlyList<string>)o.Arguments
                    };
                }),
                CommonOptions.HelpOption(),
                CommonOptions.ConfigurationOption(),
                CommonOptions.FrameworkOption(),
                Create.Option(
                    "-p|--project",
                    LocalizableStrings.CommandOptionProjectDescription,
                    Accept.ExactlyOneArgument));
    }
}