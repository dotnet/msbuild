// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;

using static Microsoft.DotNet.Tools.Format.FormatCommandCommon;

namespace Microsoft.DotNet.Tools.Format
{
    internal static partial class FormatCommandParser
    {
        public static Command GetCommand()
        {
            var formatCommand = new Command("format", LocalizableStrings.Formats_code_to_match_editorconfig_settings)
            {
                FormatWhitespaceCommandParser.GetCommand(),
                FormatStyleCommandParser.GetCommand(),
                FormatAnalyzersCommandParser.GetCommand(),
                DiagnosticsOption,
                SeverityOption,
            };
            formatCommand.AddCommonOptions();
            return formatCommand;
        }
    }
}
