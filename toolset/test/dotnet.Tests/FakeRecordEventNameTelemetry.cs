// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.DotNet.Cli;
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

        public ConcurrentBag<LogEntry> LogEntries { get; set; } = new ConcurrentBag<LogEntry>();

        public class LogEntry
        {
            public string EventName { get; set; }
            public IDictionary<string, string> Properties { get; set; }
            public IDictionary<string, double> Measurement { get; set; }
        }
    }
}
