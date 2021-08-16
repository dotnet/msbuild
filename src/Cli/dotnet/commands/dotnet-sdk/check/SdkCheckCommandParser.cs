// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Sdk.Check
{
    internal static class SdkCheckCommandParser
    {
        public static Command GetCommand()
        {
            var command = new Command("check", LocalizableStrings.AppFullName);

            return command;
        }
    }
}
