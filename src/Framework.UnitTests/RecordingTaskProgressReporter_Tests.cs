// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class RecordingTaskProgressReporter_Tests
{
    [Fact]
    public void MockEngineRecordsEveryCreatedReporter()
    {
        var engine = new MockEngine();
        EngineServices services = ((IBuildEngine10)engine).EngineServices;

        ITaskProgressReporter first = services.CreateTaskProgressReporter("Downloading", TaskProgressUnit.Bytes);
        ITaskProgressReporter second = services.CreateTaskProgressReporter("Extracting", TaskProgressUnit.Items);
        first.Complete();
        second.Dispose();

        engine.TaskProgressReporters.Count.ShouldBe(2);
        engine.TaskProgressReporters[0].ShouldSatisfyAllConditions(
            r => r.Title.ShouldBe("Downloading"),
            r => r.Unit.ShouldBe(TaskProgressUnit.Bytes),
            r => r.Outcome.ShouldBe(TaskProgressOutcome.Completed));
        engine.TaskProgressReporters[1].ShouldSatisfyAllConditions(
            r => r.Title.ShouldBe("Extracting"),
            r => r.Outcome.ShouldBe(TaskProgressOutcome.Abandoned));
    }

    [Fact]
    public void RecorderFollowsEngineContract()
    {
        using var reporter = new RecordingTaskProgressReporter("Restoring", TaskProgressUnit.Items);

        reporter.Report(new TaskProgressUpdate(0));
        reporter.SetTotal(3);
        reporter.SetTotal(3);
        reporter.Increment();
        reporter.SetStatus("Resolving");

        reporter.Updates.Count.ShouldBe(3);
        reporter.LatestUpdate.ShouldSatisfyAllConditions(
            u => u.Completed.ShouldBe(1),
            u => u.Total.ShouldBe(3),
            u => u.Status.ShouldBe("Resolving"));

        reporter.Fail("broken");
        reporter.Complete("ignored");
        reporter.Increment();
        reporter.Dispose();

        reporter.Outcome.ShouldBe(TaskProgressOutcome.Failed);
        reporter.Summary.ShouldBe("broken");
        reporter.Completed.ShouldBe(1);
    }

    [Fact]
    public void RecorderUsesStatusProviderForFinalSummary()
    {
        using var reporter = new RecordingTaskProgressReporter("Restoring", TaskProgressUnit.Items);
        string status = "Resolving 2";

        reporter.SetStatus("explicit");
        reporter.SetStatusProvider(() => status);
        reporter.Status.ShouldBe("Resolving 2");

        status = "Up to date";
        reporter.Complete();

        reporter.Summary.ShouldBe("Up to date");
        reporter.StatusProvider.ShouldBeNull();
    }

    [Fact]
    public void RecorderFallsBackWhenStatusProviderThrows()
    {
        using var reporter = new RecordingTaskProgressReporter("Restoring", TaskProgressUnit.Items);

        reporter.SetStatus("explicit");
        reporter.SetStatusProvider(() => throw new InvalidOperationException());

        reporter.Status.ShouldBe("explicit");
        Should.NotThrow(() => reporter.Complete());
        reporter.Summary.ShouldBe("explicit");
    }

    [Fact]
    public void RecorderRecordsNestedReportersAndAbandonsActiveOnesWithParent()
    {
        using var reporter = new RecordingTaskProgressReporter("Restoring", TaskProgressUnit.Items);

        using ITaskProgressReporter completed = reporter.CreateNestedReporter("Completed", TaskProgressUnit.Items, TaskProgressNestedRetention.Persist);
        using ITaskProgressReporter active = reporter.CreateNestedReporter("Active");
        completed.Complete();
        reporter.Complete();
        using ITaskProgressReporter late = reporter.CreateNestedReporter("Late");
        late.Report(new TaskProgressUpdate(1));

        reporter.NestedReporters.Count.ShouldBe(2);
        reporter.NestedReporters[0].Title.ShouldBe("Completed");
        reporter.NestedReporters[0].Retention.ShouldBe(TaskProgressNestedRetention.Persist);
        reporter.NestedReporters[0].IsComplete.ShouldBeTrue();
        reporter.NestedReporters[1].IsAbandoned.ShouldBeTrue();
        ((RecordingTaskProgressReporter)late).Updates.ShouldBeEmpty();
    }
}
