// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Telemetry
{
    internal class TelemetryFilter : ITelemetryFilter
    {
        private const string DotnetName = "dotnet";

        public IEnumerable<ApplicationInsightsEntryFormat> Filter(object objectToFilter)
        {
            var ruleSet = new List<IParseResultLogRule>
            {   new AllowListToSendFirstArgument(new HashSet<string>{ "new", "help" }),
                new AllowListToSendFirstAppliedOptions(new HashSet<string>{ "add", "remove", "list", "sln", "nuget" }),
                new TopLevelCommandNameAndOptionToLog
                (
                    topLevelCommandName: new HashSet<string> { "new" },
                    optionsToLog: new HashSet<string> { "language" }
                ),
                new TopLevelCommandNameAndOptionToLog
                (
                    topLevelCommandName: new HashSet<string> { "build", "publish" },
                    optionsToLog: new HashSet<string> { "framework", "runtime", "configuration" }
                ),
                new TopLevelCommandNameAndOptionToLog
                (
                    topLevelCommandName: new HashSet<string> { "run", "clean", "test" },
                    optionsToLog: new HashSet<string> { "framework", "configuration" }
                ),
                new TopLevelCommandNameAndOptionToLog
                (
                    topLevelCommandName: new HashSet<string> { "pack" },
                    optionsToLog: new HashSet<string> { "configuration" }
                ),
                new TopLevelCommandNameAndOptionToLog
                (
                    topLevelCommandName: new HashSet<string> { "migrate" },
                    optionsToLog: new HashSet<string> { "sdk-package-version" }
                ),
                new TopLevelCommandNameAndOptionToLog
                (
                    topLevelCommandName: new HashSet<string> { "vstest" },
                    optionsToLog: new HashSet<string> { "platform", "framework", "logger" }
                ),
                new TopLevelCommandNameAndOptionToLog
                (
                    topLevelCommandName: new HashSet<string> { "publish" },
                    optionsToLog: new HashSet<string> { "runtime" }
                )
            };
            var result = new List<ApplicationInsightsEntryFormat>();

            if (objectToFilter is ParseResult parseResult)
            {
                var topLevelCommandName = parseResult[DotnetName]?.AppliedOptions?.FirstOrDefault()?.Name;
                if (topLevelCommandName != null)
                {
                    LogVerbosityForAllTopLevelCommand(result, parseResult, topLevelCommandName);

                    foreach (IParseResultLogRule rule in ruleSet)
                    {
                        result.AddRange(rule.AllowList(parseResult));
                    }
                }
            }

            return result;
        }

        private static void LogVerbosityForAllTopLevelCommand(
            ICollection<ApplicationInsightsEntryFormat> result,
            ParseResult parseResult,
            string topLevelCommandName)
        {
            if (parseResult[DotnetName][topLevelCommandName]?.AppliedOptions != null &&
                parseResult[DotnetName][topLevelCommandName].AppliedOptions.Contains("verbosity"))
            {
                AppliedOption appliedOptions =
                    parseResult[DotnetName][topLevelCommandName].AppliedOptions["verbosity"];

                result.Add(new ApplicationInsightsEntryFormat(
                    "dotnet-" + topLevelCommandName,
                    new Dictionary<string, string>()
                    {
                        {"verbosity", appliedOptions.Arguments.ElementAt(0)}
                    }));
            }
        }
    }
}
