// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAAllowEmptyTelemetry
    {
        [Theory]
        [InlineData("Property1")]
        [InlineData("Property1=")]
        public void WhenInvokeWithoutValueItSendValueAsNull(string eventData)
        {
            var engine = new MockBuildEngine5();

            AllowEmptyTelemetry telemetryTask = new AllowEmptyTelemetry
            {
                BuildEngine = engine,
                EventName = "My event name",
                EventData = eventData
            };

            telemetryTask.Execute();

            engine.Log.Should().Contain("'Property1' = 'null'");
        }

        [Fact]
        public void WhenInvokeWithDuplicatedEventDataItKeepsTheLastOne()
        {
            var engine = new MockBuildEngine5();

            AllowEmptyTelemetry telemetryTask = new AllowEmptyTelemetry
            {
                BuildEngine = engine,
                EventName = "My event name",
                EventData = "Property1=EE2493A167D24F00996DE7C8E769EAE6;Property1=4ADE3D2622CA400B8B95A039DF540037"
            };

            bool retVal = telemetryTask.Execute();

            retVal.Should().BeTrue();

            engine.Log.Should().NotContain("EE2493A167D24F00996DE7C8E769EAE6");
            engine.Log.Should().Contain("4ADE3D2622CA400B8B95A039DF540037");
        }

        [Fact]
        public void WhenInvokeWithInvalidEventDataItThrows()
        {
            var engine = new MockBuildEngine5();

            AllowEmptyTelemetry telemetryTask = new AllowEmptyTelemetry
            {
                BuildEngine = engine,
                EventName = "My event name",
                EventData = "Property1=Value1;=Value2"
            };

            Action a = () => telemetryTask.Execute();

            a.ShouldThrow<ArgumentException>();
        }

        [Fact]
        public void WhenInvokeWithNoEventDataItSendsEvents()
        {
            var engine = new MockBuildEngine5();

            AllowEmptyTelemetry telemetryTask = new AllowEmptyTelemetry
            {
                BuildEngine = engine,
                EventName = "My event name"
            };

            bool retVal = telemetryTask.Execute();

            retVal.Should().BeTrue();
            engine.Log.Should().Contain(telemetryTask.EventName);
        }

        /// <summary>
        /// Only implement telemetry related API
        /// </summary>
        private class MockBuildEngine5 : MockBuildEngine, IBuildEngine5
        {
            private readonly object _lockObj = new object();
            private readonly StringBuilder _log = new StringBuilder();

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
                foreach (string key in properties?.Keys)
                {
                    message += $"  Property '{key}' = '{properties[key]}'{Environment.NewLine}";
                }

                lock (_lockObj)
                {
                    _log.AppendLine(message);
                }
            }

            public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties,
                IDictionary targetOutputs, string toolsVersion)
            {
                throw new NotImplementedException();
            }

            public bool BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames,
                IDictionary[] globalProperties,
                IDictionary[] targetOutputsPerProject, string[] toolsVersion, bool useResultsCache,
                bool unloadProjectsOnCompletion)
            {
                throw new NotImplementedException();
            }

            public bool IsRunningMultipleNodes { get; }

            public BuildEngineResult BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames,
                IDictionary[] globalProperties, IList<string>[] removeGlobalProperties, string[] toolsVersion,
                bool returnTargetOutputs)
            {
                throw new NotImplementedException();
            }

            public void Yield()
            {
                throw new NotImplementedException();
            }

            public void Reacquire()
            {
                throw new NotImplementedException();
            }

            public void RegisterTaskObject(object key, object obj, RegisteredTaskObjectLifetime lifetime,
                bool allowEarlyCollection)
            {
                throw new NotImplementedException();
            }

            public object GetRegisteredTaskObject(object key, RegisteredTaskObjectLifetime lifetime)
            {
                throw new NotImplementedException();
            }

            public object UnregisterTaskObject(object key, RegisteredTaskObjectLifetime lifetime)
            {
                throw new NotImplementedException();
            }
        }
    }
}
