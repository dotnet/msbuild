// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
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

        public List<ApplicationInsightsEntryFormat> AllowList(ParseResult parseResult)
        {
            var result = new List<ApplicationInsightsEntryFormat>();
            var topLevelCommandNameFromParse = parseResult["dotnet"]?.AppliedOptions?.FirstOrDefault()?.Name;
            if (topLevelCommandNameFromParse != null)
            {
                if (_topLevelCommandNameAllowList.Contains(topLevelCommandNameFromParse))
                {
                    var firstArgument = parseResult["dotnet"][topLevelCommandNameFromParse].Arguments
                        ?.FirstOrDefault();
                    if (firstArgument != null)
                    {
                        result.Add(new ApplicationInsightsEntryFormat(
                            "sublevelparser/command",
                            new Dictionary<string, string>
                            {
                                {"verb", topLevelCommandNameFromParse},
                                {"argument", firstArgument}
                            }));
                    }
                }
            }
            return result;
        }
    }
}
