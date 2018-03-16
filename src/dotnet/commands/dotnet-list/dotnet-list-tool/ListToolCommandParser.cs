// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.List.Tool.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ListToolCommandParser
    {
        public static Command ListTool()
        {
            return Create.Command(
                "tool",
                LocalizableStrings.CommandDescription,
                Create.Option(
                    "-g|--global",
                    LocalizableStrings.GlobalOptionDescription,
                    Accept.NoArguments()),
                Create.Option(
                    "--tool-path",
                    LocalizableStrings.ToolPathDescription,
                    Accept.ExactlyOneArgument()),
                CommonOptions.HelpOption());
        }
    }
}
