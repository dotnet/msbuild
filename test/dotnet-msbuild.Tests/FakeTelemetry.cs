// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Telemetry;
using System.Collections.Generic;

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

        public LogEntry LogEntry { get; private set; }

    }

}
