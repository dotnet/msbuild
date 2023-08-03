// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
                var secondVerb = parseResult.Tokens.Where(s => s.Type == CliTokenType.Command).Skip(1).FirstOrDefault()?.Value ?? "";

                if (TopLevelCommandNameAllowList.Contains(topLevelCommandNameFromParse))
                {
                    var firstArgument = parseResult.Tokens.FirstOrDefault(t => t.Type.Equals(CliTokenType.Argument))?.Value ?? "";
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
