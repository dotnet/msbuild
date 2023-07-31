// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using System.Globalization;

namespace Microsoft.DotNet.Cli.Telemetry
{
    internal class TelemetryFilter : ITelemetryFilter
    {
        private const string ExceptionEventName = "mainCatchException/exception";
        private readonly Func<string, string> _hash;

        public TelemetryFilter(Func<string, string> hash)
        {
            _hash = hash ?? throw new ArgumentNullException(nameof(hash));
        }

        public IEnumerable<ApplicationInsightsEntryFormat> Filter(object objectToFilter)
        {
            var result = new List<ApplicationInsightsEntryFormat>();
            Dictionary<string,double> measurements = null;
             if (objectToFilter is Tuple<ParseResult, Dictionary<string,double>> parseResultWithMeasurements)
            {
                objectToFilter = parseResultWithMeasurements.Item1;
                measurements = parseResultWithMeasurements.Item2;
                measurements = RemoveZeroTimes(measurements);
            }

            if (objectToFilter is ParseResult parseResult)
            {
                var topLevelCommandName = parseResult.RootSubCommandResult();
                if (topLevelCommandName != null)
                {
                    result.Add(new ApplicationInsightsEntryFormat(
                        "toplevelparser/command",
                        new Dictionary<string, string>()
                        {{ "verb", topLevelCommandName }}
                        , measurements
                        ));

                    LogVerbosityForAllTopLevelCommand(result, parseResult, topLevelCommandName, measurements);

                    foreach (IParseResultLogRule rule in ParseResultLogRules)
                    {
                        result.AddRange(rule.AllowList(parseResult, measurements));
                    }
                }
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
                topLevelCommandName: new HashSet<string> {"build", "publish"},
                optionsToLog: new HashSet<CliOption> { BuildCommandParser.FrameworkOption, PublishCommandParser.FrameworkOption,
                    BuildCommandParser.RuntimeOption, PublishCommandParser.RuntimeOption, BuildCommandParser.ConfigurationOption,
                    PublishCommandParser.ConfigurationOption }
            ),
            new TopLevelCommandNameAndOptionToLog
            (
                topLevelCommandName: new HashSet<string> {"run", "clean", "test"},
                optionsToLog: new HashSet<CliOption> { RunCommandParser.FrameworkOption, CleanCommandParser.FrameworkOption,
                    TestCommandParser.FrameworkOption, RunCommandParser.ConfigurationOption, CleanCommandParser.ConfigurationOption,
                    TestCommandParser.ConfigurationOption }
            ),
            new TopLevelCommandNameAndOptionToLog
            (
                topLevelCommandName: new HashSet<string> {"pack"},
                optionsToLog: new HashSet<CliOption> { PackCommandParser.ConfigurationOption }
            ),
            new TopLevelCommandNameAndOptionToLog
            (
                topLevelCommandName: new HashSet<string> {"vstest"},
                optionsToLog: new HashSet<CliOption> { CommonOptions.TestPlatformOption,
                    CommonOptions.TestFrameworkOption, CommonOptions.TestLoggerOption }
            ),
            new TopLevelCommandNameAndOptionToLog
            (
                topLevelCommandName: new HashSet<string> {"publish"},
                optionsToLog: new HashSet<CliOption> { PublishCommandParser.RuntimeOption }
            ),
            new AllowListToSendVerbSecondVerbFirstArgument(new HashSet<string> {"workload", "tool", "new"}),
        };

        private static void LogVerbosityForAllTopLevelCommand(
            ICollection<ApplicationInsightsEntryFormat> result,
            ParseResult parseResult,
            string topLevelCommandName,
            Dictionary<string, double> measurements = null)
        {
            if (parseResult.IsDotnetBuiltInCommand() &&
                parseResult.SafelyGetValueForOption(CommonOptions.VerbosityOption) is VerbosityOptions verbosity)
            {
                result.Add(new ApplicationInsightsEntryFormat(
                    "sublevelparser/command",
                    new Dictionary<string, string>()
                    {
                        { "verb", topLevelCommandName},
                        { "verbosity", Enum.GetName(verbosity)}
                    },
                    measurements));
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

        private Dictionary<string,double> RemoveZeroTimes(Dictionary<string,double> measurements)
        {
            if (measurements != null)
            {
                foreach (var measurement in measurements)
                {
                    if (measurement.Value == 0)
                    {
                        measurements.Remove(measurement.Key);
                    }
                }
                if (measurements.Count == 0)
                {
                    measurements = null;
                }
            }
            return measurements;
        }
    }
}
