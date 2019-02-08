// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Build.Shared.Debugging;
using Shouldly;
using Xunit;
using CommonWriterType = System.Action<string, string, System.Collections.Generic.IEnumerable<string>>;

namespace Microsoft.Build.UnitTests
{
    public sealed class PrintLineDebugger_Tests
    {
        private class MockWriter
        {
            public readonly List<string> Logs = new List<string>();

            public CommonWriterType Writer()
            {
                return (id, callsite, args) => Logs.Add($"{id}{callsite}{string.Join(";", args)}");
            }
        }

        private void AssertContextInfo(List<string> writerLogs, string id = "", [CallerMemberName] string memberName = "")
        {
            foreach (var log in writerLogs)
            {
                log.ShouldStartWith($"{id}@{nameof(PrintLineDebugger_Tests)}.{memberName}(");
            }
        }

        [Fact]
        public void DebuggerCanLogToWriter()
        {
            var writer = new MockWriter();

            using (var logger = PrintLineDebugger.Create(writer.Writer()))
            {
                logger.Log("Hello World");
            }

            writer.Logs.ShouldNotBeEmpty();
            writer.Logs.First().ShouldEndWith("Hello World");
            writer.Logs.ShouldHaveSingleItem();

            AssertContextInfo(writer.Logs);
        }

        [Fact]
        public void CompositeWriterCanWriteToMultipleWriters()
        {
            var writer1 = new MockWriter();
            var writer2 = new MockWriter();

            var compositeWriter = new PrintLineDebuggerWriters.CompositeWriter(
                new []
                {
                    writer1.Writer(),
                    writer2.Writer()
                });

            using (var logger = PrintLineDebugger.Create(compositeWriter.Writer))
            {
                logger.Log("Hello World");
            }

            writer1.Logs.ShouldNotBeEmpty();
            writer1.Logs.First().ShouldEndWith("Hello World");
            writer1.Logs.ShouldHaveSingleItem();

            AssertContextInfo(writer1.Logs);

            writer1.Logs.ShouldBe(writer2.Logs, Case.Sensitive);
        }

        [Fact]
        public void DebuggerCanPrependAnId()
        {
            var writer = new MockWriter();

            using (var logger = PrintLineDebugger.Create(writer.Writer(), "foo"))
            {
                logger.Log("Hello World");
            }

            writer.Logs.ShouldNotBeEmpty();
            writer.Logs.First().ShouldEndWith("Hello World");
            writer.Logs.ShouldHaveSingleItem();

            AssertContextInfo(writer.Logs, "foo");
        }

        [Fact]
        public void TestEnvironmentBasedPrintLineDebuggerShouldWork()
        {
            var writer = new MockWriter();

            // loggers should not log anything if there's no static writer set
            // this is useful to enable logging just for the duration of one test
            PrintLineDebugger.Default.Value.Log("outOfContext1");

            // This pattern is useful when debugging individual tests under CI.
            // The TestEnvironment the writer, so the logs will only be collected during the test
            // Caveat: One cannot use writers instantiated in the test in out of proc nodes. Either turn multiproc off, or set the writer in each node, probably in OutOfProcNode.Run
            using (var env = TestEnvironment.Create())
            {
                env.CreatePrintLineDebugger(writer.Writer());

                PrintLineDebugger.Default.Value.Log("inner");
            }

            PrintLineDebugger.Default.Value.Log("outOfContext2");

            writer.Logs.ShouldNotBeEmpty();
            writer.Logs[0].ShouldEndWith("inner");
            writer.Logs.Count.ShouldBe(1);

            AssertContextInfo(writer.Logs);
        }

        [Fact]
        public void DefaultDebuggerCanUseStaticallyControlledWriters()
        {
            var writer = new MockWriter();

            try
            {
                PrintLineDebugger.Default.Value.Log("outOfContext1");

                // This pattern is useful when debugging msbuild under VS / CLI, not individual tests under CI
                // The writer would be set at the start of each central and out of proc node, and the ID could be used to pick which file to dump the logs in (per process, per class emitting the log, etc)
                PrintLineDebugger.SetWriter(writer.Writer());

                PrintLineDebugger.Default.Value.Log("inner");
            }
            finally
            {
                PrintLineDebugger.UnsetWriter();
            }

            PrintLineDebugger.Default.Value.Log("outOfContext2");

            writer.Logs.ShouldNotBeEmpty();
            writer.Logs[0].ShouldEndWith("inner");
            writer.Logs.Count.ShouldBe(1);

            AssertContextInfo(writer.Logs);
        }

        [Fact]
        // This is one way to use the debugger without a TestEnvironment
        public void DefaultDebuggerShouldUseOuterDebuggerWriter()
        {
            var writer = new MockWriter();

            PrintLineDebugger.Default.Value.Log("outOfContext1");

            using (var logger = PrintLineDebugger.Create(writer.Writer()))
            {
                logger.Log("outer1");

                // this is what you'd litter throughout the codebase to gather random logs
                PrintLineDebugger.Default.Value.Log("inner");
                logger.Log("outer2");
            }

            PrintLineDebugger.Default.Value.Log("outOfContext2");

            writer.Logs.ShouldNotBeEmpty();
            writer.Logs[0].ShouldEndWith("outer1");
            writer.Logs[1].ShouldEndWith("inner");
            writer.Logs[2].ShouldEndWith("outer2");
            writer.Logs.Count.ShouldBe(3);

            AssertContextInfo(writer.Logs);
        }

        [Fact]
        public void ArtifactsDirectoryLooksGood()
        {
            var artifactsDirectory = PrintLineDebuggerWriters.ArtifactsLogDirectory;

            artifactsDirectory.ShouldNotBeNull();
            artifactsDirectory.ShouldEndWith(Path.Combine("log", "Debug"), Case.Sensitive);
            Path.IsPathRooted(artifactsDirectory).ShouldBeTrue();
            Directory.Exists(artifactsDirectory).ShouldBeTrue();
        }

        [Fact]
        public void CannotSetTwoWritersViaStaticSetters()
        {
            PrintLineDebugger.SetWriter(new MockWriter().Writer());
            PrintLineDebugger.UnsetWriter();

            PrintLineDebugger.SetWriter(new MockWriter().Writer());

            using (var env = TestEnvironment.Create())
            {
                env.SetEnvironmentVariable("MSBUILDDONOTLAUNCHDEBUGGER", "1");

                Should.Throw<Exception>(
                    () =>
                    {
                        PrintLineDebugger.SetWriter(new MockWriter().Writer());
                    });
            }

            PrintLineDebugger.UnsetWriter();
            PrintLineDebugger.SetWriter(new MockWriter().Writer());
            PrintLineDebugger.UnsetWriter();
        }

        [Fact]
        public void CannotSetWriterDuringADebuggerWhichAlreadySetAWriter()
        {
            PrintLineDebugger.SetWriter(new MockWriter().Writer());
            PrintLineDebugger.UnsetWriter();

            using (var env = TestEnvironment.Create())
            {
                env.SetEnvironmentVariable("MSBUILDDONOTLAUNCHDEBUGGER", "1");
                env.CreatePrintLineDebugger(new MockWriter().Writer());

                Should.Throw<Exception>(
                    () =>
                    {
                        PrintLineDebugger.SetWriter(new MockWriter().Writer());
                    });
            }

            PrintLineDebugger.SetWriter(new MockWriter().Writer());
            PrintLineDebugger.UnsetWriter();
        }

        [Fact]
        public void CannotUnsetWriterWhenNoWriterIsSet()
        {
            PrintLineDebugger.SetWriter(new MockWriter().Writer());
            PrintLineDebugger.UnsetWriter();

            using (var env = TestEnvironment.Create())
            {
                env.SetEnvironmentVariable("MSBUILDDONOTLAUNCHDEBUGGER", "1");

                Should.Throw<Exception>(
                    () =>
                    {
                        PrintLineDebugger.UnsetWriter();
                    });
            }

            PrintLineDebugger.SetWriter(new MockWriter().Writer());
            PrintLineDebugger.UnsetWriter();
        }
    }
}
#endif