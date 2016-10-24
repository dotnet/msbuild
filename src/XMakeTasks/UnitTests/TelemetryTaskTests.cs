using System;
using Microsoft.Build.UnitTests;
using Xunit;

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

            Assert.True(engine.Log.Contains(telemetryTask.EventName));
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

            Assert.True(engine.Log.Contains(propertyName));

            Assert.True(engine.Log.Contains(propertyValue));
        }
    }
}
