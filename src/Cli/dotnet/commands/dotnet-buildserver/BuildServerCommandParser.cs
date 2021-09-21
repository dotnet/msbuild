// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using LocalizableStrings = Microsoft.DotNet.Tools.BuildServer.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class BuildServerCommandParser
    {
        public static Command GetCommand()
        {
            var command = new Command("build-server", LocalizableStrings.CommandDescription);

            command.AddCommand(ServerShutdownCommandParser.GetCommand());

            command.Handler = CommandHandler.Create((Func<int>)(() => throw new Exception("TODO command not found"))); ;

            return command;
        }
    }
}
