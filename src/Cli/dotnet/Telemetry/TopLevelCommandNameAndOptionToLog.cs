// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Telemetry
{
    internal class TopLevelCommandNameAndOptionToLog : IParseResultLogRule
    {
        public TopLevelCommandNameAndOptionToLog(
            HashSet<string> topLevelCommandName,
            HashSet<string> optionsToLog)
        {
            _topLevelCommandName = topLevelCommandName;
            _optionsToLog = optionsToLog;
        }

        private HashSet<string> _topLevelCommandName { get; }
        private HashSet<string> _optionsToLog { get; }
        private const string DotnetName = "dotnet";

        public List<ApplicationInsightsEntryFormat> AllowList(ParseResult parseResult)
        {
            var topLevelCommandName = parseResult[DotnetName]?.AppliedOptions?.FirstOrDefault()?.Name;
            var result = new List<ApplicationInsightsEntryFormat>();
            foreach (var option in _optionsToLog)
            {
                if (_topLevelCommandName.Contains(topLevelCommandName)
                    && parseResult[DotnetName]?[topLevelCommandName]?.AppliedOptions != null
                    && parseResult[DotnetName][topLevelCommandName].AppliedOptions.Contains(option))
                {
                    AppliedOption appliedOptions =
                        parseResult[DotnetName][topLevelCommandName]
                            .AppliedOptions[option];
                    result.Add(new ApplicationInsightsEntryFormat(
                        "sublevelparser/command",
                        new Dictionary<string, string>
                        {
                            { "verb", topLevelCommandName},
                            {option, appliedOptions.Arguments.ElementAt(0)}
                        }));
                }
            }
            return result;
        }
    }
}
