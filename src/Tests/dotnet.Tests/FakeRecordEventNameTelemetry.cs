// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using Microsoft.DotNet.Cli.Telemetry;

namespace Microsoft.DotNet.Tests
{
    public class FakeRecordEventNameTelemetry : ITelemetry
    {
        public bool Enabled { get; set; }

        public string EventName { get; set; }

        public void TrackEvent(string eventName,
            IDictionary<string, string> properties,
            IDictionary<string, double> measurements)
        {
            LogEntries.Add(
                new LogEntry
                {
                    EventName = eventName,
                    Measurement = measurements,
                    Properties = properties
                });
        }

        public void Flush()
        {
        }

        public void Dispose()
        {
        }

        public ConcurrentBag<LogEntry> LogEntries { get; set; } = new ConcurrentBag<LogEntry>();

        public class LogEntry
        {
            public string EventName { get; set; }
            public IDictionary<string, string> Properties { get; set; }
            public IDictionary<string, double> Measurement { get; set; }
        }
    }
}
