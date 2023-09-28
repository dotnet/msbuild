// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Xunit;

#nullable enable

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAAllowEmptyTelemetry
    {
        private static ITaskItem CreateHashItem(string key, string? value = null, bool? hash = null)
        {
            var item = new TaskItem(key);
            item.SetMetadata("Value", value);
            if (hash is not null)
            {
                item.SetMetadata("Hash", hash.Value.ToString());
            }
            return item;
        }

        [Fact]
        public void WhenInvokeWithoutValueItSendValueAsNull()
        {
            var engine = new MockBuildEngine5();

            AllowEmptyTelemetry telemetryTask = new()
            {
                BuildEngine = engine,
                EventName = "My event name",
                EventData = new ITaskItem[] {
                    CreateHashItem("Property1"),
                    CreateHashItem("Property2", "")
                }
            };

            telemetryTask.Execute();

            engine.Log.Should().Contain("'Property1' = 'null'");
            engine.Log.Should().Contain("'Property2' = 'null'");
        }

        [Fact]
        public void WhenInvokeWithDuplicatedEventDataItKeepsTheLastOne()
        {
            var engine = new MockBuildEngine5();

            AllowEmptyTelemetry telemetryTask = new()
            {
                BuildEngine = engine,
                EventName = "My event name",
                EventData = new ITaskItem[] {
                    CreateHashItem("Property1", "EE2493A167D24F00996DE7C8E769EAE6"),
                    CreateHashItem("Property1", "4ADE3D2622CA400B8B95A039DF540037")
                }
            };

            bool retVal = telemetryTask.Execute();

            retVal.Should().BeTrue();

            engine.Log.Should().NotContain("EE2493A167D24F00996DE7C8E769EAE6");
            engine.Log.Should().Contain("4ADE3D2622CA400B8B95A039DF540037");
        }

        [Fact]
        public void WhenInvokeWithNoEventDataItSendsEvents()
        {
            var engine = new MockBuildEngine5();

            AllowEmptyTelemetry telemetryTask = new()
            {
                BuildEngine = engine,
                EventName = "My event name"
            };

            bool retVal = telemetryTask.Execute();

            retVal.Should().BeTrue();
            engine.Log.Should().Contain(telemetryTask.EventName);
            engine.Log.Should().NotContain("Property"); // shouldn't have any logged properties since none were supplied
        }

        [Fact]
        public void WhenHashIsRequestedValueIsHashed()
        {
            var engine = new MockBuildEngine5();

            AllowEmptyTelemetry telemetryTask = new()
            {
                BuildEngine = engine,
                EventName = "My event name",
                EventData = new ITaskItem[] {
                    CreateHashItem("Property1", "hi", true),
                    CreateHashItem("Property2", "hello", false)
                }
            };

            telemetryTask.Execute();
            // first property should be hashed
            engine.Log.Should().Contain("'Property1' = 'cd6f6854353f68f47c9c93217c5084bc66ea1af918ae1518a2d715a1885e1fcb'");
            engine.Log.Should().Contain("'Property2' = 'hello'");
        }

        /// <summary>
        /// Only implement telemetry related API
        /// </summary>
        private class MockBuildEngine5 : MockBuildEngine, IBuildEngine5
        {
            private readonly object _lockObj = new();
            private readonly StringBuilder _log = new();

            internal string Log
            {
                get
                {
                    lock (_lockObj)
                    {
                        return _log.ToString();
                    }
                }
                set
                {
                    if (!string.IsNullOrEmpty(value))
                    {
                        throw new ArgumentException("Expected log setter to be used only to reset the log to empty.");
                    }

                    lock (_lockObj)
                    {
                        _log.Clear();
                    }
                }
            }

            public void LogTelemetry(string eventName, IDictionary<string, string> properties)
            {
                string message = $"Received telemetry event '{eventName}'{Environment.NewLine}";
                if (properties is not null)
                {
                    foreach (string key in properties.Keys)
                    {
                        message += $"  Property '{key}' = '{properties[key]}'{Environment.NewLine}";
                    }
                }

                lock (_lockObj)
                {
                    _log.AppendLine(message);
                }
            }
        }
    }
}
