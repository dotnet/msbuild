// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;

using static Microsoft.DotNet.Tools.Format.FormatCommandCommon;

namespace Microsoft.DotNet.Tools.Format
{
    internal static class FormatWhitespaceCommandParser
    {
        public static Command GetCommand()
        {
            var command = new Command("whitespace", LocalizableStrings.Run_whitespace_formatting)
            {
                FolderOption,
            };
            command.AddCommonOptions();
            return command;
        }
    }
}
