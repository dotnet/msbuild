// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Cli.Telemetry;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Cli
{
    public class InternalReportinstallsuccess
    {
        internal const string TelemetrySessionIdEnvironmentVariableName = "DOTNET_CLI_TELEMETRY_SESSIONID";

        public static int Run(ParseResult parseResult)
        {
            var telemetry = new ThreadBlockingTelemetry();
            ProcessInputAndSendTelemetry(parseResult, telemetry);
            telemetry.Dispose();
            return 0;
        }

        public static void ProcessInputAndSendTelemetry(string[] args, ITelemetry telemetry)
        {
            var parser = Parser.Instance;
            var result = parser.ParseFrom("dotnet internal-reportinstallsuccess", args);
            ProcessInputAndSendTelemetry(result, telemetry);
        }

        public static void ProcessInputAndSendTelemetry(ParseResult result, ITelemetry telemetry)
        {
            var exeName = Path.GetFileName(result.GetValueForArgument(InternalReportinstallsuccessCommandParser.Argument));

            var filter = new TelemetryFilter(Sha256Hasher.HashWithNormalizedCasing);
            foreach (var e in filter.Filter(new InstallerSuccessReport(exeName)))
            {
                telemetry.TrackEvent(e.EventName, e.Properties, null);
            }
        }

        internal class ThreadBlockingTelemetry : ITelemetry
        {
            private Telemetry.Telemetry telemetry;

            internal ThreadBlockingTelemetry()
            {
                var sessionId =
                Environment.GetEnvironmentVariable(TelemetrySessionIdEnvironmentVariableName);
                telemetry = new Telemetry.Telemetry(new NoOpFirstTimeUseNoticeSentinel(), sessionId, blockThreadInitialization: true);
            }
            public bool Enabled => telemetry.Enabled;

            public void Flush()
            {
            }

            public void Dispose()
            {
                telemetry.Dispose();
            }

            public void TrackEvent(string eventName, IDictionary<string, string> properties, IDictionary<string, double> measurements)
            {
                telemetry.ThreadBlockingTrackEvent(eventName, properties, measurements);
            }
        }
    }

    internal class InstallerSuccessReport
    {
        public string ExeName { get; }

        public InstallerSuccessReport(string exeName)
        {
            ExeName = exeName ?? throw new ArgumentNullException(nameof(exeName));
        }
    }
}
