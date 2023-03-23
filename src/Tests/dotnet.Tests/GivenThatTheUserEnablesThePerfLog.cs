// Copyright (c) .NET Foundation and contributors. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 

using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Configurer;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using System.Runtime.CompilerServices;
using System.Diagnostics.Tracing;

namespace Microsoft.DotNet.Tests
{
    public class GivenThatTheUserEnablesThePerfLog : SdkTest
    {
        public GivenThatTheUserEnablesThePerfLog(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void WhenPerfLogDisabledDotNetDoesNotWriteToThePerfLog()
        {
            var dir = _testAssetsManager.CreateTestDirectory();

            var result = new DotnetCommand(Log, "--help")
                .WithEnvironmentVariable("DOTNET_PERFLOG_DIR", dir.Path)
                .Execute();

            result.ExitCode.Should().Be(0);
            Assert.Empty(new DirectoryInfo(dir.Path).GetFiles());
        }

        [Fact]
        public void WhenPerfLogEnabledDotNetWritesToThePerfLog()
        {
            var dir = _testAssetsManager.CreateTestDirectory();

            var result = new DotnetCommand(Log, "--help")
                .WithEnvironmentVariable("DOTNET_CLI_PERF_LOG", "1")
                .WithEnvironmentVariable("DOTNET_PERFLOG_DIR", dir.Path)
                .Execute();

            result.ExitCode.Should().Be(0);

            DirectoryInfo logDir = new DirectoryInfo(dir.Path);
            FileInfo[] logFiles = logDir.GetFiles();
            Assert.NotEmpty(logFiles);
            Assert.All(logFiles, f => Assert.StartsWith("perf-", f.Name));
            Assert.All(logFiles, f => Assert.NotEqual(0, f.Length));
        }

        [Fact]
        public void WhenPerfLogEnabledDotNetBuildWritesAPerfLog()
        {
            using (PerfLogTestEventListener listener = new PerfLogTestEventListener())
            {
                int exitCode = Cli.Program.Main(new string[] { "--help" });
                Assert.Equal(0, exitCode);
                Assert.NotEqual(0, listener.EventCount);
            }
        }
    }

    internal sealed class PerfLogTestEventListener : EventListener
    {
        private const string PerfLogEventSourceName = "Microsoft-Dotnet-CLI-Performance";

        public int EventCount
        {
            get; private set;
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if(eventSource.Name.Equals(PerfLogEventSourceName))
            {
                EnableEvents(eventSource, EventLevel.Verbose);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            Assert.Equal(PerfLogEventSourceName, eventData.EventSource.Name);
            EventCount++;
        }
    }
}
