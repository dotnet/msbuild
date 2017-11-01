// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Xunit;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.Build.Framework;
using System.Collections.Generic;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public class GivenMSBuildLogger
    {
        [Fact(DisplayName = "It blocks telemetry that is not in the list")]
        public void ItBlocks()
        {
            var fakeTelemetry = new FakeTelemetry();
            var telemetryEventArgs = new TelemetryEventArgs
            {
                EventName = "User Defined Event Name",
                Properties = new Dictionary<string, string>
                {
                    { "User Defined Key", "User Defined Value"},
                }
            };

            MSBuildLogger.FormatAndSend(fakeTelemetry, telemetryEventArgs);

            fakeTelemetry.LogEntry.Should().BeNull();
        }

        [Fact(DisplayName = "It masks event name with targetframeworkeval only on TargetFrameworkVersion")]
        public void ItMasksTargetFrameworkEventname()
        {
            var fakeTelemetry = new FakeTelemetry();
            var telemetryEventArgs = new TelemetryEventArgs
            {
                EventName = "targetframeworkeval",
                Properties = new Dictionary<string, string>
            {
                { "TargetFrameworkVersion", ".NETStandard,Version=v2.0"},
            }
            };

            MSBuildLogger.FormatAndSend(fakeTelemetry, telemetryEventArgs);

            fakeTelemetry.LogEntry.EventName.Should().Be("msbuild/targetframeworkeval");
            fakeTelemetry.LogEntry.Properties.Keys.Count.Should().Be(1);
            var expectedKey = "TargetFrameworkVersion";
            fakeTelemetry.LogEntry.Properties.Should().ContainKey(expectedKey);
            fakeTelemetry.LogEntry.Properties[expectedKey].Should().Be(Sha256Hasher.Hash(".NETSTANDARD,VERSION=V2.0"));
        }

        public class FakeTelemetry : ITelemetry
        {
            public bool Enabled { get; set; }

            public void TrackEvent(string eventName, IDictionary<string, string> properties, IDictionary<string, double> measurements)
            {
                LogEntry = new LogEntry { EventName = eventName, Properties = properties, Measurement = measurements };

            }

            public LogEntry LogEntry { get; private set; }

        }

        public class LogEntry
        {
            public string EventName { get; set; }
            public IDictionary<string, string> Properties { get; set; }
            public IDictionary<string, double> Measurement { get; set; }
        }
    }
}
