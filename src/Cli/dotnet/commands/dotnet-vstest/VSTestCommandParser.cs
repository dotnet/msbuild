// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;

namespace Microsoft.DotNet.Cli
{
    internal static class VSTestCommandParser
    {
        public static Command GetCommand()
        {
            var command = new Command("vstest");

            return command;
        }
    }
}
