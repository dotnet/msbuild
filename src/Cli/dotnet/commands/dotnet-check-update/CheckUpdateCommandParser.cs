// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.CheckUpdate.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class CheckUpdateCommandParser
    {
        // TODO:
        //      Add entry to top level help
        //      Add docs + docs link to BuiltInCommandsCatalog


        public static Command GetCommand()
        {
            var command = new Command("check-update", LocalizableStrings.AppFullName);

            return command;
        }
    }
}
