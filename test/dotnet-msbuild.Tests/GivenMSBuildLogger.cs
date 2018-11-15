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
        [Fact]
        public void ItBlocksTelemetryThatIsNotInTheList()
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

        [Fact]
        public void ItMasksEventNameWithTargetframeworkevalOnTargetFrameworkVersionUseWindowsFormsOrWPF()
        {
            var fakeTelemetry = new FakeTelemetry();
            var telemetryEventArgs = new TelemetryEventArgs
            {
                EventName = MSBuildLogger.TargetFrameworkTelemetryEventName,
                Properties = new Dictionary<string, string>
            {
                { MSBuildLogger.TargetFrameworkVersionTelemetryPropertyKey, ".NETStandard,Version=v2.0"},
                { MSBuildLogger.UseWindowsFormsTelemetryPropertyKey, "true"},
                { MSBuildLogger.UseWPFTelemetryPropertyKey, "AnyNonTrueValue"},
            }
            };

            MSBuildLogger.FormatAndSend(fakeTelemetry, telemetryEventArgs);

            fakeTelemetry.LogEntry.EventName.Should().Be($"msbuild/{MSBuildLogger.TargetFrameworkTelemetryEventName}");
            fakeTelemetry.LogEntry.Properties.Keys.Count.Should().Be(3);
            fakeTelemetry.LogEntry.Properties[MSBuildLogger.TargetFrameworkVersionTelemetryPropertyKey].Should().Be(Sha256Hasher.Hash(".NETSTANDARD,VERSION=V2.0"));
            fakeTelemetry.LogEntry.Properties[MSBuildLogger.UseWindowsFormsTelemetryPropertyKey].Should().Be("True");
            fakeTelemetry.LogEntry.Properties[MSBuildLogger.UseWPFTelemetryPropertyKey]
                .Should().Be(
                "False",
                "sanitize to avoid user input, and since in SDK prop and target non 'true' is effectively false");
        }

        [Fact]
        public void ItMasksEventNameWithTargetframeworkevalOnTargetFrameworkVersionUseWindowsFormsOrWPFWhenFieldIsEmpty()
        {
            var fakeTelemetry = new FakeTelemetry();
            var telemetryEventArgs = new TelemetryEventArgs
            {
                EventName = MSBuildLogger.TargetFrameworkTelemetryEventName,
                Properties = new Dictionary<string, string>
            {
                { MSBuildLogger.UseWindowsFormsTelemetryPropertyKey, "null"},
            }
            };

            MSBuildLogger.FormatAndSend(fakeTelemetry, telemetryEventArgs);

            fakeTelemetry.LogEntry.Properties[MSBuildLogger.UseWindowsFormsTelemetryPropertyKey]
                .Should().Be(
                "null",
                "MSBuild will throw when the task param contain empty and if the field is empty json will emit the entry, so it still need to be set to something.");
        }

    }
}
