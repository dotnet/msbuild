// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli
{
    internal static class UpdateCommandParser
    {
        public static Command Update()
        {
            return Create.Command(
                "update",
                Tools.Update.LocalizableStrings.CommandDescription,
                Accept.NoArguments(),
                CommonOptions.HelpOption(),
                UpdateToolCommandParser.Update());
        }
    }
}
