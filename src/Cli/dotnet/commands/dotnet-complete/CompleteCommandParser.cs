// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli
{
    internal static class CompleteCommandParser
    {
        public static Command Complete() =>
            Create.Command(
                "complete", "",
                Accept.ExactlyOneArgument()
                      .With(name: "path"),
                Create.Option("--position", "",
                              Accept.ExactlyOneArgument()
                                    .With(name: "command")
                                    .MaterializeAs(o => int.Parse(o.Arguments.Single()))));
    }
}