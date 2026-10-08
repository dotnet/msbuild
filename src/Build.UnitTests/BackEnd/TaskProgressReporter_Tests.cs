// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
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
        public void AbandonRemainingClosesManagerAgainstLaterReporters()
        {
            var manager = new TaskProgressManager();
            var forwarded = new System.Collections.Generic.List<BuildEventArgs>();

            manager.AbandonRemaining();
            ITaskProgressReporter reporter = manager.CreateReporter("Late", TaskProgressUnit.Unspecified, BuildEventContext.Invalid, forwarded.Add);

            reporter.Report(new TaskProgressUpdate(1, 2));
            reporter.Complete();

            forwarded.ShouldBeEmpty();
        }

        [Fact]
        public void ReentrantAbandonDuringStartedEventForwardsTerminalEventAfterStart()
        {
            var manager = new TaskProgressManager();
            var forwarded = new System.Collections.Generic.List<BuildEventArgs>();

            manager.CreateReporter(
                "Starting",
                TaskProgressUnit.Unspecified,
                BuildEventContext.Invalid,
                e =>
                {
                    forwarded.Add(e);
                    if (e is TaskProgressStartedEventArgs)
                    {
                        manager.AbandonRemaining();
                    }
                });

            forwarded.Select(e => e.GetType()).ShouldBe([typeof(TaskProgressStartedEventArgs), typeof(TaskProgressFinishedEventArgs)]);
            forwarded.OfType<TaskProgressFinishedEventArgs>().ShouldHaveSingleItem().Outcome.ShouldBe(TaskProgressOutcome.Abandoned);
        }

        [Fact]
        public void AbandonRemainingCannotMissReporterDuringStartedEvent()
        {
            var manager = new TaskProgressManager();
            var forwarded = new System.Collections.Concurrent.ConcurrentQueue<BuildEventArgs>();
            using var started = new ManualResetEventSlim();
            using var continueStart = new ManualResetEventSlim();
            using var teardownStarted = new ManualResetEventSlim();

            Task<ITaskProgressReporter> creating = Task.Run(() => manager.CreateReporter(
                "Creating",
                TaskProgressUnit.Unspecified,
                BuildEventContext.Invalid,
                e =>
                {
                    forwarded.Enqueue(e);
                    if (e is TaskProgressStartedEventArgs)
                    {
                        started.Set();
                        continueStart.Wait();
                    }
                }));

            started.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            Task abandoning = Task.Run(() =>
            {
                teardownStarted.Set();
                manager.AbandonRemaining();
            });

            try
            {
                teardownStarted.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
                abandoning.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            }
            finally
            {
                continueStart.Set();
            }

            Task.WaitAll(creating, abandoning);

            ((TaskProgressReporter)creating.Result).IsActive.ShouldBeFalse();
            forwarded.ToArray().Select(e => e.GetType()).ShouldBe([typeof(TaskProgressStartedEventArgs), typeof(TaskProgressFinishedEventArgs)]);
            forwarded.ToArray().OfType<TaskProgressFinishedEventArgs>().ShouldHaveSingleItem().Outcome.ShouldBe(TaskProgressOutcome.Abandoned);
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
        public void NestedReporterForwardsParentAndRetention()
        {
            var manager = new TaskProgressManager();
            var forwarded = new System.Collections.Generic.List<BuildEventArgs>();
            var parent = (TaskProgressReporter)manager.CreateReporter("Restoring projects", TaskProgressUnit.Items, BuildEventContext.Invalid, forwarded.Add);

            var nested = (TaskProgressReporter)parent.CreateNestedReporter("Installing packages", TaskProgressUnit.Items, TaskProgressNestedRetention.Persist);

            nested.OperationId.ShouldNotBe(parent.OperationId);
            TaskProgressStartedEventArgs[] started = forwarded.OfType<TaskProgressStartedEventArgs>().ToArray();
            started.Length.ShouldBe(2);
            started[0].ParentOperationId.ShouldBe(0);
            started[1].OperationId.ShouldBe(nested.OperationId);
            started[1].ParentOperationId.ShouldBe(parent.OperationId);
            started[1].Retention.ShouldBe(TaskProgressNestedRetention.Persist);
            started[1].Title.ShouldBe("Installing packages");
        }

        [Fact]
        public void NestedReporterEndsIndependentlyOfParent()
        {
            var manager = new TaskProgressManager();
            var forwarded = new System.Collections.Generic.List<BuildEventArgs>();
            ITaskProgressReporter parent = manager.CreateReporter("Restoring projects", TaskProgressUnit.Items, BuildEventContext.Invalid, forwarded.Add);
            var nested = (TaskProgressReporter)parent.CreateNestedReporter("Installing packages");

            nested.Complete();
            parent.Report(new TaskProgressUpdate(1, 2));

            nested.IsActive.ShouldBeFalse();
            ((TaskProgressReporter)parent).IsActive.ShouldBeTrue();
            forwarded.OfType<TaskProgressFinishedEventArgs>().ShouldHaveSingleItem().OperationId.ShouldBe(nested.OperationId);
        }

        [Fact]
        public void EndingParentAbandonsActiveNestedReportersFirst()
        {
            var manager = new TaskProgressManager();
            var forwarded = new System.Collections.Generic.List<BuildEventArgs>();
            var parent = (TaskProgressReporter)manager.CreateReporter("Restoring projects", TaskProgressUnit.Items, BuildEventContext.Invalid, forwarded.Add);
            var completed = (TaskProgressReporter)parent.CreateNestedReporter("Completed nested");
            var active = (TaskProgressReporter)parent.CreateNestedReporter("Active nested");
            var grandchild = (TaskProgressReporter)active.CreateNestedReporter("Grandchild");
            completed.Complete();

            parent.Complete();

            active.IsActive.ShouldBeFalse();
            grandchild.IsActive.ShouldBeFalse();
            TaskProgressFinishedEventArgs[] finished = forwarded.OfType<TaskProgressFinishedEventArgs>().ToArray();
            finished.Select(e => e.OperationId).ShouldBe([completed.OperationId, grandchild.OperationId, active.OperationId, parent.OperationId]);
            finished[1].Outcome.ShouldBe(TaskProgressOutcome.Abandoned);
            finished[2].Outcome.ShouldBe(TaskProgressOutcome.Abandoned);
            finished[3].Outcome.ShouldBe(TaskProgressOutcome.Completed);
        }

        [Fact]
        public void NestedReporterOfEndedParentIgnoresEveryCall()
        {
            var manager = new TaskProgressManager();
            var forwarded = new System.Collections.Generic.List<BuildEventArgs>();
            ITaskProgressReporter parent = manager.CreateReporter("Restoring projects", TaskProgressUnit.Items, BuildEventContext.Invalid, forwarded.Add);
            parent.Complete();
            int count = forwarded.Count;

            ITaskProgressReporter nested = parent.CreateNestedReporter("Late");
            nested.Report(new TaskProgressUpdate(1, 2));
            nested.CreateNestedReporter("Later").Complete();
            nested.Complete();

            forwarded.Count.ShouldBe(count);
        }

        [Fact]
        public void NestedReporterRequiresTitle()
        {
            var manager = new TaskProgressManager();
            using ITaskProgressReporter parent = manager.CreateReporter("Restoring projects", TaskProgressUnit.Items, BuildEventContext.Invalid);

            Should.Throw<ArgumentNullException>(() => parent.CreateNestedReporter(null!));
            Should.Throw<ArgumentException>(() => parent.CreateNestedReporter(" "));
        }

        [Fact]
        public void AbandonRemainingEndsLeakedNestedReportersBeforeTheirParent()
        {
            var manager = new TaskProgressManager();
            var forwarded = new System.Collections.Generic.List<BuildEventArgs>();
            var parent = (TaskProgressReporter)manager.CreateReporter("Restoring projects", TaskProgressUnit.Items, BuildEventContext.Invalid, forwarded.Add);
            var nested = (TaskProgressReporter)parent.CreateNestedReporter("Installing packages");

            manager.AbandonRemaining();

            forwarded.OfType<TaskProgressFinishedEventArgs>().Select(e => e.OperationId).ShouldBe([nested.OperationId, parent.OperationId]);
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
        public void ThrottledUpdateIsForwardedOnTrailingEdge()
        {
            var manager = new TaskProgressManager();
            var forwarded = new System.Collections.Concurrent.ConcurrentQueue<BuildEventArgs>();
            using var flushed = new ManualResetEventSlim();
            ITaskProgressReporter reporter = manager.CreateReporter(
                "Restoring",
                TaskProgressUnit.Items,
                BuildEventContext.Invalid,
                e =>
                {
                    forwarded.Enqueue(e);
                    if (e is TaskProgressUpdatedEventArgs { Status: "Waiting" })
                    {
                        flushed.Set();
                    }
                });

            reporter.Report(new TaskProgressUpdate(1, 10, "Working"));
            reporter.Report(new TaskProgressUpdate(2, 10, "Waiting"));

            // The second report falls inside the throttle window. With no further reports, it must
            // still arrive once the window closes rather than being held until the terminal event.
            flushed.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();

            reporter.Complete();
            TaskProgressUpdatedEventArgs last = forwarded.ToArray().OfType<TaskProgressUpdatedEventArgs>().Last();
            last.Completed.ShouldBe(2);
            last.Status.ShouldBe("Waiting");
        }

        [Fact]
        public void TrailingFlushDoesNotDuplicateOrOutliveTerminalTransition()
        {
            var manager = new TaskProgressManager();
            var forwarded = new System.Collections.Concurrent.ConcurrentQueue<BuildEventArgs>();
            ITaskProgressReporter reporter = manager.CreateReporter("Restoring", TaskProgressUnit.Items, BuildEventContext.Invalid, forwarded.Enqueue);

            reporter.Report(new TaskProgressUpdate(1, 10));
            reporter.Report(new TaskProgressUpdate(2, 10));
            reporter.Complete();

            // Wait well past the throttle window; the cancelled flush must not emit anything.
            Thread.Sleep(500);

            BuildEventArgs[] events = forwarded.ToArray();
            events[events.Length - 1].ShouldBeOfType<TaskProgressFinishedEventArgs>().Completed.ShouldBe(2);
            events.OfType<TaskProgressUpdatedEventArgs>().Count().ShouldBeLessThanOrEqualTo(2);
        }

        [Fact]
        public void IdenticalUpdatesAreNotAcceptedOrForwarded()
        {
            var manager = new TaskProgressManager();
            var forwarded = new System.Collections.Concurrent.ConcurrentQueue<BuildEventArgs>();
            ITaskProgressReporter reporter = manager.CreateReporter("Restoring", TaskProgressUnit.Items, BuildEventContext.Invalid, forwarded.Enqueue);

            reporter.Report(new TaskProgressUpdate(1, 10, "Resolving"));
            long sequence = ((TaskProgressReporter)reporter).Sequence;

            // Outside the throttle window, so any forward would be immediate.
            Thread.Sleep(300);
            reporter.Report(new TaskProgressUpdate(1, 10, "Resolving"));

            ((TaskProgressReporter)reporter).Sequence.ShouldBe(sequence);
            forwarded.OfType<TaskProgressUpdatedEventArgs>().Count().ShouldBe(1);
        }

        [Fact]
        public void InitialZeroUpdateIsNotForwarded()
        {
            var manager = new TaskProgressManager();
            var forwarded = new System.Collections.Concurrent.ConcurrentQueue<BuildEventArgs>();
            ITaskProgressReporter reporter = manager.CreateReporter("Restoring", TaskProgressUnit.Items, BuildEventContext.Invalid, forwarded.Enqueue);

            // Begin already implies zero progress with no total and no status.
            reporter.Report(new TaskProgressUpdate(0));

            forwarded.OfType<TaskProgressUpdatedEventArgs>().ShouldBeEmpty();
        }

        [Fact]
        public void ThrottledChangeRevertedToForwardedValueIsNotFlushed()
        {
            var manager = new TaskProgressManager();
            var forwarded = new System.Collections.Concurrent.ConcurrentQueue<BuildEventArgs>();
            ITaskProgressReporter reporter = manager.CreateReporter("Restoring", TaskProgressUnit.Items, BuildEventContext.Invalid, forwarded.Enqueue);

            reporter.Report(new TaskProgressUpdate(1, 10, "Resolving"));
            reporter.Report(new TaskProgressUpdate(1, 10, "Installing"));
            reporter.Report(new TaskProgressUpdate(1, 10, "Resolving"));

            // Wait past the throttle window: the trailing flush must see that the latest state is the one
            // already forwarded and emit nothing.
            Thread.Sleep(500);

            forwarded.OfType<TaskProgressUpdatedEventArgs>().Count().ShouldBe(1);
        }

        [Fact]
        public void ForwardingFailuresNeverEscapeTheReporter()
        {
            var manager = new TaskProgressManager();
            int attempts = 0;
            using var flushAttempted = new ManualResetEventSlim();
            ITaskProgressReporter? reporter = null;

            Should.NotThrow(() => reporter = manager.CreateReporter(
                "Restoring",
                TaskProgressUnit.Items,
                BuildEventContext.Invalid,
                e =>
                {
                    if (Interlocked.Increment(ref attempts) == 3)
                    {
                        // Begin, the first update, and the trailing flush have all been attempted.
                        flushAttempted.Set();
                    }

                    throw new InvalidOperationException("logging failed");
                }));

            Should.NotThrow(() => reporter!.Report(new TaskProgressUpdate(1, 10)));
            Should.NotThrow(() => reporter!.Report(new TaskProgressUpdate(2, 10)));

            // The trailing flush runs on a thread-pool thread; an escaping exception would terminate the process.
            flushAttempted.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();

            Should.NotThrow(() => reporter!.Complete("done"));
            Should.NotThrow(() => reporter!.Fail("again"));
            Should.NotThrow(() => reporter!.Report(new TaskProgressUpdate(3, 10)));
            Should.NotThrow(() => reporter!.Dispose());
            ((TaskProgressReporter)reporter!).IsActive.ShouldBeFalse();
        }

        [Fact]
        public void ConcurrentIncrementsAreNotLost()
        {
            var manager = new TaskProgressManager();
            TaskProgressReporter reporter = (TaskProgressReporter)manager.CreateReporter("Restoring", TaskProgressUnit.Items, BuildEventContext.Invalid, _ => { });

            Parallel.For(0, 1000, i =>
            {
                reporter.AddToTotal(2);
                reporter.Increment();
                reporter.Increment();
            });

            reporter.LatestUpdate.Completed.ShouldBe(2000);
            reporter.LatestUpdate.Total.ShouldBe(2000);
        }

        [Fact]
        public void CounterAndStatusSettersKeepOtherFields()
        {
            var manager = new TaskProgressManager();
            TaskProgressReporter reporter = (TaskProgressReporter)manager.CreateReporter("Restoring", TaskProgressUnit.Items, BuildEventContext.Invalid);

            reporter.SetTotal(10);
            reporter.SetStatus("Resolving");
            reporter.Increment(3);
            reporter.LatestUpdate.ShouldSatisfyAllConditions(
                u => u.Completed.ShouldBe(3),
                u => u.Total.ShouldBe(10),
                u => u.Status.ShouldBe("Resolving"));

            reporter.AddToTotal(5);
            reporter.SetStatus(null);
            reporter.LatestUpdate.Total.ShouldBe(15);
            reporter.LatestUpdate.Status.ShouldBeNull();
            reporter.LatestUpdate.Completed.ShouldBe(3);

            reporter.SetTotal(null);
            reporter.LatestUpdate.Total.ShouldBeNull();
            reporter.AddToTotal(4);
            reporter.LatestUpdate.Total.ShouldBe(4);
        }

        [Fact]
        public void CountersClampInsteadOfThrowing()
        {
            var manager = new TaskProgressManager();
            TaskProgressReporter reporter = (TaskProgressReporter)manager.CreateReporter("Restoring", TaskProgressUnit.Items, BuildEventContext.Invalid);

            reporter.Increment(-5);
            reporter.LatestUpdate.Completed.ShouldBe(0);

            reporter.Increment(long.MaxValue);
            reporter.Increment(long.MaxValue);
            reporter.LatestUpdate.Completed.ShouldBe(long.MaxValue);

            reporter.SetTotal(-1);
            reporter.LatestUpdate.Total.ShouldBe(0);
            reporter.AddToTotal(-3);
            reporter.LatestUpdate.Total.ShouldBe(0);
        }

        [Fact]
        public void CounterChangesUseDeduplicationAndIgnoreClosedReporter()
        {
            var manager = new TaskProgressManager();
            var forwarded = new System.Collections.Concurrent.ConcurrentQueue<BuildEventArgs>();
            TaskProgressReporter reporter = (TaskProgressReporter)manager.CreateReporter("Restoring", TaskProgressUnit.Items, BuildEventContext.Invalid, forwarded.Enqueue);

            reporter.SetStatus("Resolving");
            long sequence = reporter.Sequence;
            reporter.SetStatus("Resolving");
            reporter.Increment(0);
            reporter.Sequence.ShouldBe(sequence);

            reporter.Complete();
            reporter.Increment();
            reporter.AddToTotal(1);
            reporter.SetTotal(5);
            reporter.SetStatus("Late");
            reporter.LatestUpdate.Completed.ShouldBe(0);
            reporter.LatestUpdate.Status.ShouldBe("Resolving");
            forwarded.OfType<TaskProgressUpdatedEventArgs>().Count().ShouldBe(1);
        }

        [Fact]
        public void StatusProviderIsPolledAndSuppliesForwardedAndFinalStatus()
        {
            var manager = new TaskProgressManager();
            var forwarded = new System.Collections.Concurrent.ConcurrentQueue<BuildEventArgs>();
            ITaskProgressReporter reporter = manager.CreateReporter("Restoring", TaskProgressUnit.Items, BuildEventContext.Invalid, forwarded.Enqueue);

            string status = "Resolving 3";
            reporter.SetTotal(10);
            reporter.SetStatusProvider(() => Volatile.Read(ref status));

            // The provider changes without any call on the reporter; polling must pick it up.
            WaitFor(() => forwarded.OfType<TaskProgressUpdatedEventArgs>().Any(u => u.Status == "Resolving 3"));
            Volatile.Write(ref status, "Installing 2");
            WaitFor(() => forwarded.OfType<TaskProgressUpdatedEventArgs>().Any(u => u.Status == "Installing 2"));

            Volatile.Write(ref status, "All done");
            reporter.Complete();

            TaskProgressFinishedEventArgs finished = forwarded.OfType<TaskProgressFinishedEventArgs>().Single();
            finished.Summary.ShouldBe("All done");
            finished.Total.ShouldBe(10);

            long[] sequences = forwarded.OfType<TaskProgressUpdatedEventArgs>().Select(u => u.Sequence).ToArray();
            sequences.ShouldBe(sequences.Distinct().OrderBy(s => s));
            sequences.ShouldAllBe(s => s < finished.Sequence);
        }

        [Fact]
        public void TerminalSummaryOverridesStatusProvider()
        {
            var manager = new TaskProgressManager();
            var forwarded = new System.Collections.Concurrent.ConcurrentQueue<BuildEventArgs>();
            ITaskProgressReporter reporter = manager.CreateReporter("Restoring", TaskProgressUnit.Items, BuildEventContext.Invalid, forwarded.Enqueue);

            reporter.SetStatusProvider(() => "computed");
            reporter.Fail("summary");

            forwarded.OfType<TaskProgressFinishedEventArgs>().Single().Summary.ShouldBe("summary");
        }

        [Fact]
        public void FailingStatusProviderFallsBackToExplicitStatus()
        {
            var manager = new TaskProgressManager();
            var forwarded = new System.Collections.Concurrent.ConcurrentQueue<BuildEventArgs>();
            ITaskProgressReporter reporter = manager.CreateReporter("Restoring", TaskProgressUnit.Items, BuildEventContext.Invalid, forwarded.Enqueue);

            reporter.Report(new TaskProgressUpdate(1, 5, "explicit"));
            int calls = 0;
            reporter.SetStatusProvider(() =>
            {
                Interlocked.Increment(ref calls);
                throw new InvalidOperationException("provider failed");
            });

            WaitFor(() => Volatile.Read(ref calls) > 0);
            Thread.Sleep(400);
            Volatile.Read(ref calls).ShouldBe(1);

            reporter.Complete();
            forwarded.OfType<TaskProgressFinishedEventArgs>().Single().Summary.ShouldBe("explicit");
        }

        [Fact]
        public void ClearingStatusProviderRestoresExplicitStatus()
        {
            var manager = new TaskProgressManager();
            var forwarded = new System.Collections.Concurrent.ConcurrentQueue<BuildEventArgs>();
            ITaskProgressReporter reporter = manager.CreateReporter("Restoring", TaskProgressUnit.Items, BuildEventContext.Invalid, forwarded.Enqueue);

            reporter.SetStatus("explicit");
            reporter.SetStatusProvider(() => "computed");
            WaitFor(() => forwarded.OfType<TaskProgressUpdatedEventArgs>().Any(u => u.Status == "computed"));

            reporter.SetStatusProvider(null);
            WaitFor(() => forwarded.OfType<TaskProgressUpdatedEventArgs>().LastOrDefault()?.Status == "explicit");
            reporter.Complete();

            forwarded.OfType<TaskProgressFinishedEventArgs>().Single().Summary.ShouldBe("explicit");
        }

        private static void WaitFor(Func<bool> condition)
        {
            SpinWait.SpinUntil(condition, TimeSpan.FromSeconds(10)).ShouldBeTrue();
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
