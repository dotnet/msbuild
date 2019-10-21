// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.BuildServer.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class BuildServerCommandParser
    {
        public static Command CreateCommand()
        {
            return Create.Command(
                "build-server",
                LocalizableStrings.CommandDescription,
                Accept.NoArguments(),
                CommonOptions.HelpOption(),
                ServerShutdownCommandParser.CreateCommand());
        }
    }
}
