// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolCommandParser
    {
        public static Command Tool()
        {
            return Create.Command(
                "tool",
                LocalizableStrings.CommandDescription,
                Accept.NoArguments(),
                CommonOptions.HelpOption(),
                ToolInstallCommandParser.ToolInstall(),
                ToolUninstallCommandParser.ToolUninstall(),
                ToolUpdateCommandParser.ToolUpdate(),
                ToolListCommandParser.ToolList(),
                ToolRunCommandParser.ToolRun(),
                ToolRestoreCommandParser.ToolRestore());
        }
    }
}
