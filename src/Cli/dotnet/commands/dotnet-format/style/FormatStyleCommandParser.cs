// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;

using static Microsoft.DotNet.Tools.Format.FormatCommandCommon;

namespace Microsoft.DotNet.Tools.Format
{
    internal static class FormatStyleCommandParser
    {
        public static Command GetCommand()
        {
            var command = new Command("style", LocalizableStrings.Run_code_style_analyzers_and_apply_fixes)
            {
                DiagnosticsOption,
                SeverityOption,
            };
            command.AddCommonOptions();
            return command;
        }
    }
}
