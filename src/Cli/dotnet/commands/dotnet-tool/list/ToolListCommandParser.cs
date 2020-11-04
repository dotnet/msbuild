// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using Microsoft.DotNet.Tools.Tool.Common;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.List.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolListCommandParser
    {
        public static readonly Option GlobalOption = ToolAppliedOption.GlobalOption;

        public static readonly Option LocalOption = ToolAppliedOption.LocalOption;

        public static readonly Option ToolPathOption = ToolAppliedOption.ToolPathOption;

        public static Command GetCommand()
        {
            var command = new Command("list", LocalizableStrings.CommandDescription);

            command.AddOption(GlobalOption);
            command.AddOption(LocalOption);
            command.AddOption(ToolPathOption);

            return command;
        }
    }
}
