// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Telemetry
{
    internal class TopLevelCommandNameAndOptionToLog : IParseResultLogRule
    {
        public TopLevelCommandNameAndOptionToLog(
            HashSet<string> topLevelCommandName,
            HashSet<Option> optionsToLog)
        {
            _topLevelCommandName = topLevelCommandName;
            _optionsToLog = optionsToLog;
        }

        private HashSet<string> _topLevelCommandName { get; }
        private HashSet<Option> _optionsToLog { get; }

        public List<ApplicationInsightsEntryFormat> AllowList(ParseResult parseResult, Dictionary<string, double> measurements = null)
        {
            var topLevelCommandName = parseResult.RootSubCommandResult();
            var result = new List<ApplicationInsightsEntryFormat>();
            foreach (var option in _optionsToLog)
            {
                if (_topLevelCommandName.Contains(topLevelCommandName)
                    && parseResult.HasOption(option))
                {
                    result.Add(new ApplicationInsightsEntryFormat(
                        "sublevelparser/command",
                        new Dictionary<string, string>
                        {
                            { "verb", topLevelCommandName},
                            { option.Name, parseResult.GetValueForOption(option)?.ToString() }
                        },
                        measurements));
                }
            }
            return result;
        }
    }
}
