// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Telemetry
{
    internal class AllowListToSendVerbSecondVerbFirstArgument : IParseResultLogRule
    {
        public AllowListToSendVerbSecondVerbFirstArgument(
            HashSet<string> topLevelCommandNameAllowList)
        {
            TopLevelCommandNameAllowList = topLevelCommandNameAllowList;
        }

        private HashSet<string> TopLevelCommandNameAllowList { get; }

        public List<ApplicationInsightsEntryFormat> AllowList(ParseResult parseResult, Dictionary<string, double> measurements = null)
        {
            var result = new List<ApplicationInsightsEntryFormat>();
            var topLevelCommandNameFromParse = parseResult.RootSubCommandResult();

            if (topLevelCommandNameFromParse != null)
            {
                var secondVerb = parseResult.Tokens.Where(s => s.Type == TokenType.Command).Skip(1).FirstOrDefault()?.Value ?? "";

                if (TopLevelCommandNameAllowList.Contains(topLevelCommandNameFromParse))
                {
                    var firstArgument = parseResult.Tokens.FirstOrDefault(t => t.Type.Equals(TokenType.Argument))?.Value ?? "";
                    if (secondVerb != null)
                    {
                        result.Add(new ApplicationInsightsEntryFormat(
                            "sublevelparser/command",
                            new Dictionary<string, string>
                            {
                                {"verb", topLevelCommandNameFromParse},
                                {"subcommand", secondVerb},
                                {"argument", firstArgument}
                            },
                            measurements));
                    }
                }
            }
            return result;
        }
    }
}
