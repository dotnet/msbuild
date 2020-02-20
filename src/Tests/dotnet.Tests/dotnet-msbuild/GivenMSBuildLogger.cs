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
        public void ItDoesNotMasksExceptionTelemetry()
        {
            var fakeTelemetry = new FakeTelemetry();
            var telemetryEventArgs = new TelemetryEventArgs
            {
                EventName = MSBuildLogger.SdkTaskBaseCatchExceptionTelemetryEventName,
                Properties = new Dictionary<string, string>
                {
                    { "exceptionType", "System.Exception"},
                    { "detail", "Exception detail"}
                }
            };

            MSBuildLogger.FormatAndSend(fakeTelemetry, telemetryEventArgs);

            fakeTelemetry.LogEntry.EventName.Should().Be(MSBuildLogger.SdkTaskBaseCatchExceptionTelemetryEventName);
            fakeTelemetry.LogEntry.Properties.Keys.Count.Should().Be(2);
            fakeTelemetry.LogEntry.Properties["exceptionType"].Should().Be("System.Exception");
            fakeTelemetry.LogEntry.Properties["detail"].Should().Be("Exception detail");
        }
    }
}
