// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.BuildServer.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class BuildServerCommandParser
    {
        public static Command GetCommand()
        {
            var command = new Command("build-server", LocalizableStrings.CommandDescription);

            command.AddCommand(ServerShutdownCommandParser.GetCommand());

            return command;
        }
    }
}
