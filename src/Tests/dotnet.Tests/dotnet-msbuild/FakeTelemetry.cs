// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Telemetry;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public class FakeTelemetry : ITelemetry
    {
        public bool Enabled { get; set; }

        public void TrackEvent(string eventName, IDictionary<string, string> properties, IDictionary<string, double> measurements)
        {
            LogEntry = new LogEntry { EventName = eventName, Properties = properties, Measurement = measurements };

        }

        public void Flush()
        {
        }

        public void Dispose()
        {
        }

        public LogEntry LogEntry { get; private set; }

    }

}
