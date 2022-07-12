// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Telemetry
{
    internal class AllowListToSendFirstArgument : IParseResultLogRule
    {
        public AllowListToSendFirstArgument(
            HashSet<string> topLevelCommandNameAllowList)
        {
            _topLevelCommandNameAllowList = topLevelCommandNameAllowList;
        }

        private HashSet<string> _topLevelCommandNameAllowList { get; }

        public List<ApplicationInsightsEntryFormat> AllowList(ParseResult parseResult, Dictionary<string, double> measurements = null)
        {
            var result = new List<ApplicationInsightsEntryFormat>();
            var topLevelCommandNameFromParse = parseResult.RootCommandResult.Children.FirstOrDefault()?.Symbol.Name;
            if (topLevelCommandNameFromParse != null)
            {
                if (_topLevelCommandNameAllowList.Contains(topLevelCommandNameFromParse))
                {
                    var firstArgument = parseResult.RootCommandResult.Children.FirstOrDefault()?.Tokens.Where(t => t.Type.Equals(TokenType.Argument)).FirstOrDefault()?.Value ?? null;
                    if (firstArgument != null)
                    {
                        result.Add(new ApplicationInsightsEntryFormat(
                            "sublevelparser/command",
                            new Dictionary<string, string>
                            {
                                {"verb", topLevelCommandNameFromParse},
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
