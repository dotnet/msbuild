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
    public sealed class PrintLineDebugger_Tests : IDisposable
    {
        private readonly TestEnvironment _env;

        public PrintLineDebugger_Tests()
        {
            PrintLineDebugger.GetStaticWriter().ShouldBeNull();

            _env = TestEnvironment.Create();
        }

        private class MockWriter
        {
            private readonly string _writerId;
            public readonly List<string> Logs = new List<string>();

            public MockWriter(string writerId = "")
            {
                _writerId = writerId;
            }

            public CommonWriterType Writer()
            {
                return (id, callsite, args) => Logs.Add($"{_writerId}{id}{callsite}{string.Join(";", args)}");
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
            var artifactsDirectory = RepositoryInfo.Instance.ArtifactsLogDirectory;

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

            _env.SetEnvironmentVariable("MSBUILDDONOTLAUNCHDEBUGGER", "1");

            Should.Throw<Exception>(
                () =>
                {
                    PrintLineDebugger.SetWriter(new MockWriter().Writer());
                });

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

            _env.SetEnvironmentVariable("MSBUILDDONOTLAUNCHDEBUGGER", "1");

            Should.Throw<Exception>(
                () =>
                {
                    PrintLineDebugger.UnsetWriter();
                });

            PrintLineDebugger.SetWriter(new MockWriter().Writer());
            PrintLineDebugger.UnsetWriter();
        }

        [Fact]
        public void CreateWithFallBackWriterSetsWriterIfNoWriterIsSet()
        {
            var writer = new MockWriter("FallBackWriter");

            using (var debugger = PrintLineDebugger.CreateWithFallBackWriter(writer.Writer()))
            {
                debugger.Log("foo");
            }

            writer.Logs.ShouldHaveSingleItem();
            writer.Logs.First().ShouldEndWith("foo");
            writer.Logs.First().ShouldStartWith("FallbackWriter");
        }

        [Fact]
        public void CreateWithFallBackWriterDoesNotSetWriterIfAWriterIsAlreadySet()
        {
            try
            {
                var firstWriter = new MockWriter("FirstWriter");
                var fallbackWriter = new MockWriter("FallBackWriter");

                PrintLineDebugger.SetWriter(firstWriter.Writer());

                PrintLineDebugger.Default.Value.Log("ForFirstWriter1");

                using (var debugger = PrintLineDebugger.CreateWithFallBackWriter(fallbackWriter.Writer()))
                {
                    debugger.Log("foo");
                    PrintLineDebugger.Default.Value.Log("ForFirstWriter2");
                }

                PrintLineDebugger.Default.Value.Log("ForFirstWriter3");

                fallbackWriter.Logs.ShouldBeEmpty();

                firstWriter.Logs.Count.ShouldBe(4);

                firstWriter.Logs.ShouldAllBe(message => message.StartsWith("FirstWriter"));

                firstWriter.Logs[0].ShouldEndWith("ForFirstWriter1");
                firstWriter.Logs[1].ShouldEndWith("foo");
                firstWriter.Logs[2].ShouldEndWith("ForFirstWriter2");
                firstWriter.Logs[3].ShouldEndWith("ForFirstWriter3");
            }
            finally
            {
                PrintLineDebugger.UnsetWriter();
            }
        }

        public void Dispose()
        {
            _env?.Dispose();
        }
    }
}
#endif
