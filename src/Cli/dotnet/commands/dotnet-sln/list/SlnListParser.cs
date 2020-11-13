// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Sln.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    public static class SlnListParser
    {
        public static Command GetCommand()
        {
            var command = new Command("list", LocalizableStrings.ListAppFullName);

            return command;
        }
    }
}
