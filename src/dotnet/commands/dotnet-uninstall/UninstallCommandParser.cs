// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Uninstall.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class UninstallCommandParser
    {
        public static Command Uninstall()
        {
            return Create.Command(
                "uninstall",
                LocalizableStrings.CommandDescription,
                Accept.NoArguments(),
                CommonOptions.HelpOption(),
                UninstallToolCommandParser.UninstallTool());
        }
    }
}
