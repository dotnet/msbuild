// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
            var topLevelCommandNameFromParse = parseResult.RootCommandResult.Children.FirstOrDefault() switch
            {
                System.CommandLine.Parsing.CommandResult commandResult => commandResult.Command.Name,
                OptionResult optionResult => optionResult.Option.Name,
                ArgumentResult argumentResult => argumentResult.Argument.Name,
                _ => null
            };
            if (topLevelCommandNameFromParse != null)
            {
                if (_topLevelCommandNameAllowList.Contains(topLevelCommandNameFromParse))
                {
                    var firstArgument = parseResult.RootCommandResult.Children.FirstOrDefault()?.Tokens.Where(t => t.Type.Equals(CliTokenType.Argument)).FirstOrDefault()?.Value ?? null;
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
