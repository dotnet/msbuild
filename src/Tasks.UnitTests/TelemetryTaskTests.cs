﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.UnitTests;
using Xunit;

#nullable disable

namespace Microsoft.Build.Tasks.UnitTests
{
    public sealed class TelemetryTaskTests
    {
        [Fact]
        public void TelemetryTaskSendsEvents()
        {
            MockEngine engine = new MockEngine();

            Telemetry telemetryTask = new Telemetry
            {
                BuildEngine = engine,
                EventName = "My event name",
            };

            bool retVal = telemetryTask.Execute();

            Assert.True(retVal);

            Assert.Contains(telemetryTask.EventName, engine.Log);
        }

        [Fact]
        public void TelemetryTaskSendsEventsWithProperties()
        {
            const string propertyName = "9B7DA92A89914E2CA1D88DCEB9DAAD72";
            const string propertyValue = "68CB99868F2843B3B75230C1A0BFE358";

            MockEngine engine = new MockEngine();

            Telemetry telemetryTask = new Telemetry
            {
                BuildEngine = engine,
                EventName = "My event name",
                EventData = $"{propertyName}={propertyValue}",
            };

            bool retVal = telemetryTask.Execute();

            Assert.True(retVal);

            Assert.Contains(propertyName, engine.Log);

            Assert.Contains(propertyValue, engine.Log);
        }

        [Fact]
        public void TelemetryTaskInvalidEventData()
        {
            MockEngine engine = new MockEngine();

            Telemetry telemetryTask = new Telemetry
            {
                BuildEngine = engine,
                EventName = "My event name",
                EventData = $"Property1=Value1;Property2",
            };

            bool retVal = telemetryTask.Execute();

            Assert.False(retVal);
            Assert.Contains($"The property \"Property2\" in the telemetry event data property list \"{telemetryTask.EventData}\" is malformed.", engine.Log);
        }

        /// <summary>
        /// Verifies that when there are duplicate property names specified that the last one wins.
        /// </summary>
        [Fact]
        public void TelemetryTaskDuplicateEventDataProperty()
        {
            MockEngine engine = new MockEngine();

            Telemetry telemetryTask = new Telemetry
            {
                BuildEngine = engine,
                EventName = "My event name",
                EventData = $"Property1=EE2493A167D24F00996DE7C8E769EAE6;Property1=4ADE3D2622CA400B8B95A039DF540037",
            };

            bool retVal = telemetryTask.Execute();

            Assert.True(retVal);

            // Should not contain the first value
            //
            Assert.DoesNotContain("EE2493A167D24F00996DE7C8E769EAE6", engine.Log);

            // Should contain the second value
            //
            Assert.Contains("4ADE3D2622CA400B8B95A039DF540037", engine.Log);
        }
    }
}
