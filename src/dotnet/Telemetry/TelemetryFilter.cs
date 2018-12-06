// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Telemetry
{
    internal class TelemetryFilter : ITelemetryFilter
    {
        private const string DotnetName = "dotnet";
        private readonly Func<string, string> _hash;

        public TelemetryFilter(Func<string, string> hash)
        {
            _hash = hash ?? throw new ArgumentNullException(nameof(hash));
        }

        public IEnumerable<ApplicationInsightsEntryFormat> Filter(object objectToFilter)
        {
            var result = new List<ApplicationInsightsEntryFormat>();

            if (objectToFilter is ParseResult parseResult)
            {
                var topLevelCommandName = parseResult[DotnetName]?.AppliedOptions?.FirstOrDefault()?.Name;
                if (topLevelCommandName != null)
                {
                    LogVerbosityForAllTopLevelCommand(result, parseResult, topLevelCommandName);

                    foreach (IParseResultLogRule rule in ParseResultLogRules)
                    {
                        result.AddRange(rule.AllowList(parseResult));
                    }
                }
            }
            else if(objectToFilter is TopLevelCommandParserResult topLevelCommandParserResult)
            {
                result.Add(new ApplicationInsightsEntryFormat(
                            "toplevelparser/command",
                            new Dictionary<string, string>()
                        {{ "verb", topLevelCommandParserResult.Command}}
                ));

            }
            else if (objectToFilter is InstallerSuccessReport installerSuccessReport)
            {
                result.Add(new ApplicationInsightsEntryFormat(
                    "install/reportsuccess",
                    new Dictionary<string, string> {{ "exeName", installerSuccessReport.ExeName}}
                ));
            }

            return result.Select(r => r.WithAppliedToPropertiesValue(_hash)).ToList();
        }

        private static List<IParseResultLogRule> ParseResultLogRules => new List<IParseResultLogRule>
        {
            new AllowListToSendFirstArgument(new HashSet<string> {"new", "help"}),
            new AllowListToSendFirstAppliedOptions(new HashSet<string> {"add", "remove", "list", "sln", "nuget"}),
            new TopLevelCommandNameAndOptionToLog
            (
                topLevelCommandName: new HashSet<string> {"new"},
                optionsToLog: new HashSet<string> {"language"}
            ),
            new TopLevelCommandNameAndOptionToLog
            (
                topLevelCommandName: new HashSet<string> {"build", "publish"},
                optionsToLog: new HashSet<string> {"framework", "runtime", "configuration"}
            ),
            new TopLevelCommandNameAndOptionToLog
            (
                topLevelCommandName: new HashSet<string> {"run", "clean", "test"},
                optionsToLog: new HashSet<string> {"framework", "configuration"}
            ),
            new TopLevelCommandNameAndOptionToLog
            (
                topLevelCommandName: new HashSet<string> {"pack"},
                optionsToLog: new HashSet<string> {"configuration"}
            ),
            new TopLevelCommandNameAndOptionToLog
            (
                topLevelCommandName: new HashSet<string> {"migrate"},
                optionsToLog: new HashSet<string> {"sdk-package-version"}
            ),
            new TopLevelCommandNameAndOptionToLog
            (
                topLevelCommandName: new HashSet<string> {"vstest"},
                optionsToLog: new HashSet<string> {"platform", "framework", "logger"}
            ),
            new TopLevelCommandNameAndOptionToLog
            (
                topLevelCommandName: new HashSet<string> {"publish"},
                optionsToLog: new HashSet<string> {"runtime"}
            )
        };

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
                    "sublevelparser/command",
                    new Dictionary<string, string>()
                    {
                        { "verb", topLevelCommandName},
                        {"verbosity", appliedOptions.Arguments.ElementAt(0)}
                    }));
            }
        }
    }
}
