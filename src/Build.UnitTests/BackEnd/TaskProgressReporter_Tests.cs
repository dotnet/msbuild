// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

#nullable enable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Tests the internal task progress reporter lifecycle state machine and its owning manager.
    /// </summary>
    public class TaskProgressReporter_Tests
    {
        [Fact]
        public void CreateReporterRequiresTitle()
        {
            var manager = new TaskProgressManager();

            Should.Throw<ArgumentNullException>(() => manager.CreateReporter(null!, TaskProgressUnit.Unspecified, BuildEventContext.Invalid));
            Should.Throw<ArgumentException>(() => manager.CreateReporter(string.Empty, TaskProgressUnit.Unspecified, BuildEventContext.Invalid));
            Should.Throw<ArgumentException>(() => manager.CreateReporter(" ", TaskProgressUnit.Unspecified, BuildEventContext.Invalid));
        }

        [Fact]
        public void ReportUpdatesLatestValueUntilTerminal()
        {
            var manager = new TaskProgressManager();
            TaskProgressReporter reporter = (TaskProgressReporter)manager.CreateReporter("Downloading", TaskProgressUnit.Bytes, BuildEventContext.Invalid);

            reporter.Report(new TaskProgressUpdate(10, 100));
            reporter.Report(new TaskProgressUpdate(50, 100));

            reporter.LatestUpdate.Completed.ShouldBe(50);
            reporter.LatestUpdate.Total.ShouldBe(100);
            reporter.IsActive.ShouldBeTrue();
        }

        [Fact]
        public void CompleteClosesReporterAndIgnoresLaterReports()
        {
            var manager = new TaskProgressManager();
            TaskProgressReporter reporter = (TaskProgressReporter)manager.CreateReporter("Downloading", TaskProgressUnit.Bytes, BuildEventContext.Invalid);

            reporter.Report(new TaskProgressUpdate(50, 100));
            reporter.Complete("Done");

            reporter.IsActive.ShouldBeFalse();
            reporter.LatestUpdate.Status.ShouldBe("Done");

            // Reports after a terminal transition must be ignored, not throw, and must not reopen the reporter.
            reporter.Report(new TaskProgressUpdate(999, 100));
            reporter.LatestUpdate.Completed.ShouldBe(50);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void OnlyOneTerminalTransitionWins(bool completeFirst)
        {
            var manager = new TaskProgressManager();
            TaskProgressReporter reporter = (TaskProgressReporter)manager.CreateReporter("Downloading", TaskProgressUnit.Bytes, BuildEventContext.Invalid);

            if (completeFirst)
            {
                reporter.Complete("First");
                reporter.Cancel("Second");
            }
            else
            {
                reporter.Cancel("First");
                reporter.Complete("Second");
            }

            reporter.LatestUpdate.Status.ShouldBe("First");
        }

        [Fact]
        public void DisposeWithoutTerminalCallAbandonsReporter()
        {
            var manager = new TaskProgressManager();
            ITaskProgressReporter reporter = manager.CreateReporter("Downloading", TaskProgressUnit.Bytes, BuildEventContext.Invalid);

            reporter.Dispose();

            ((TaskProgressReporter)reporter).IsActive.ShouldBeFalse();
        }

        [Fact]
        public void DisposeAfterTerminalCallIsNoOp()
        {
            var manager = new TaskProgressManager();
            ITaskProgressReporter reporter = manager.CreateReporter("Downloading", TaskProgressUnit.Bytes, BuildEventContext.Invalid);

            reporter.Complete("Done");
            reporter.Dispose();

            ((TaskProgressReporter)reporter).LatestUpdate.Status.ShouldBe("Done");
        }

        [Fact]
        public void AbandonRemainingClosesLeakedReportersOnly()
        {
            var manager = new TaskProgressManager();
            TaskProgressReporter leaked = (TaskProgressReporter)manager.CreateReporter("Leaked", TaskProgressUnit.Unspecified, BuildEventContext.Invalid);
            TaskProgressReporter closed = (TaskProgressReporter)manager.CreateReporter("Closed", TaskProgressUnit.Unspecified, BuildEventContext.Invalid);

            closed.Complete("Done");
            manager.AbandonRemaining();

            leaked.IsActive.ShouldBeFalse();
            closed.LatestUpdate.Status.ShouldBe("Done");
        }

        [Fact]
        public void AbandonRemainingIsIdempotentAndSafeWithNoActiveReporters()
        {
            var manager = new TaskProgressManager();

            manager.AbandonRemaining();
            manager.AbandonRemaining();
        }

        [Fact]
        public void ConcurrentReportsDoNotCorruptStateAndTerminalTransitionWins()
        {
            var manager = new TaskProgressManager();
            TaskProgressReporter reporter = (TaskProgressReporter)manager.CreateReporter("Downloading", TaskProgressUnit.Bytes, BuildEventContext.Invalid);

            using var barrier = new Barrier(2);

            Task reportingTask = Task.Run(() =>
            {
                barrier.SignalAndWait();
                for (int i = 0; i < 1000; i++)
                {
                    reporter.Report(new TaskProgressUpdate(i, 1000));
                }
            });

            Task completingTask = Task.Run(() =>
            {
                barrier.SignalAndWait();
                reporter.Complete("Done");
            });

            Task.WaitAll(reportingTask, completingTask);

            reporter.IsActive.ShouldBeFalse();
            reporter.LatestUpdate.Status.ShouldBe("Done");
        }

        [Fact]
        public void OperationIdsAreUniquePerReporter()
        {
            var manager = new TaskProgressManager();
            var first = (TaskProgressReporter)manager.CreateReporter("First", TaskProgressUnit.Unspecified, BuildEventContext.Invalid);
            var second = (TaskProgressReporter)manager.CreateReporter("Second", TaskProgressUnit.Unspecified, BuildEventContext.Invalid);

            first.OperationId.ShouldNotBe(second.OperationId);
        }

        [Fact]
        public void CreatingReporterForwardsStartedEvent()
        {
            var manager = new TaskProgressManager();
            var forwarded = new System.Collections.Generic.List<BuildEventArgs>();

            manager.CreateReporter("Downloading", TaskProgressUnit.Bytes, BuildEventContext.Invalid, forwarded.Add);

            BuildEventArgs single = forwarded.ShouldHaveSingleItem();
            TaskProgressStartedEventArgs started = single.ShouldBeOfType<TaskProgressStartedEventArgs>();
            started.Title.ShouldBe("Downloading");
            started.Unit.ShouldBe(TaskProgressUnit.Bytes);
        }

        [Fact]
        public void TerminalTransitionAlwaysForwardsRegardlessOfThrottle()
        {
            var manager = new TaskProgressManager();
            var forwarded = new System.Collections.Generic.List<BuildEventArgs>();
            ITaskProgressReporter reporter = manager.CreateReporter("Downloading", TaskProgressUnit.Bytes, BuildEventContext.Invalid, forwarded.Add);

            // Back-to-back reports without waiting out the throttle window: at most the first is forwarded.
            reporter.Report(new TaskProgressUpdate(1, 100));
            reporter.Report(new TaskProgressUpdate(2, 100));
            reporter.Complete("Done");

            forwarded.Count.ShouldBeLessThanOrEqualTo(3);
            BuildEventArgs last = forwarded[forwarded.Count - 1];
            TaskProgressFinishedEventArgs finished = last.ShouldBeOfType<TaskProgressFinishedEventArgs>();
            finished.Outcome.ShouldBe(TaskProgressOutcome.Completed);
            finished.Completed.ShouldBe(2);
        }

        [Fact]
        public void NullLogEventDelegateIsSafe()
        {
            var manager = new TaskProgressManager();
            ITaskProgressReporter reporter = manager.CreateReporter("Downloading", TaskProgressUnit.Bytes, BuildEventContext.Invalid, logEvent: null);

            reporter.Report(new TaskProgressUpdate(50, 100));
            reporter.Complete("Done");

            ((TaskProgressReporter)reporter).IsActive.ShouldBeFalse();
        }
    }
}
