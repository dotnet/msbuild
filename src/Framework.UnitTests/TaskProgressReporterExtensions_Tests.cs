// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class TaskProgressReporterExtensions_Tests
{
    [Fact]
    public void FinishCompletesOnSuccess()
    {
        using var reporter = new RecordingTaskProgressReporter();

        reporter.Finish(succeeded: true);

        reporter.IsComplete.ShouldBeTrue();
        reporter.IsFailed.ShouldBeFalse();
        reporter.IsCanceled.ShouldBeFalse();
    }

    [Fact]
    public void FinishFailsOnFailure()
    {
        using var reporter = new RecordingTaskProgressReporter();

        reporter.Finish(succeeded: false, CancellationToken.None, "broken");

        reporter.IsFailed.ShouldBeTrue();
        reporter.IsComplete.ShouldBeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FinishCancelsWhenTokenIsCanceled(bool succeeded)
    {
        using var reporter = new RecordingTaskProgressReporter();
        using var source = new CancellationTokenSource();
        source.Cancel();

        reporter.Finish(succeeded, source.Token);

        reporter.IsCanceled.ShouldBeTrue();
        reporter.IsComplete.ShouldBeFalse();
        reporter.IsFailed.ShouldBeFalse();
    }
}
