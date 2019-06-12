// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using System.Globalization;

namespace Microsoft.DotNet.Cli.Telemetry
{
    internal class TelemetryFilter : ITelemetryFilter
    {
        private const string DotnetName = "dotnet";
        private const string ExceptionEventName = "mainCatchException/exception";
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
            else if (objectToFilter is TopLevelCommandParserResult topLevelCommandParserResult)
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
                    new Dictionary<string, string> { { "exeName", installerSuccessReport.ExeName } }
                ));
            }
            else if (objectToFilter is Exception exception)
            {
                result.Add(new ApplicationInsightsEntryFormat(
                    ExceptionEventName,
                    new Dictionary<string, string>
                    {
                        {"exceptionType", exception.GetType().ToString()},
                        {"detail", ExceptionToStringWithoutMessage(exception) }
                    }
                ));
            }

            return result
                .Select(r =>
                {
                    if (r.EventName == ExceptionEventName)
                    {
                        return r;
                    }
                    else
                    {
                        return r.WithAppliedToPropertiesValue(_hash);
                    }
                }).ToList();
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

        private static string ExceptionToStringWithoutMessage(Exception e)
        {
            const string AggregateException_ToString = "{0}{1}---> (Inner Exception #{2}) {3}{4}{5}";
            if (e is AggregateException aggregate)
            {
                string text = NonAggregateExceptionToStringWithoutMessage(aggregate);

                for (int i = 0; i < aggregate.InnerExceptions.Count; i++)
                {
                    text = string.Format(CultureInfo.InvariantCulture,
                                         AggregateException_ToString,
                                         text,
                                         Environment.NewLine,
                                         i,
                                         ExceptionToStringWithoutMessage(aggregate.InnerExceptions[i]),
                                         "<---",
                                         Environment.NewLine);
                }

                return text;
            }
            else
            {
                return NonAggregateExceptionToStringWithoutMessage(e);
            }
        }

        private static string NonAggregateExceptionToStringWithoutMessage(Exception e)
        {
            string s;
            const string Exception_EndOfInnerExceptionStack = "--- End of inner exception stack trace ---";


            s = e.GetType().ToString();

            if (e.InnerException != null)
            {
                s = s + " ---> " + ExceptionToStringWithoutMessage(e.InnerException) + Environment.NewLine +
                "   " + Exception_EndOfInnerExceptionStack;

            }

            var stackTrace = e.StackTrace;

            if (stackTrace != null)
            {
                s += Environment.NewLine + stackTrace;
            }

            return s;
        }
    }
}
