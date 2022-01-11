// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using Microsoft.DotNet.Tools.Sdk.Check;
using LocalizableStrings = Microsoft.DotNet.Tools.Sdk.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class SdkCommandParser
    {
        public static Command GetCommand()
        {
            var command = new Command("sdk", LocalizableStrings.AppFullName);
            command.AddCommand(SdkCheckCommandParser.GetCommand());

            return command;
        }
    }
}
