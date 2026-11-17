// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Execution;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

namespace Microsoft.Build.BuildCheck.UnitTests;

public class BuildCheckReplayModeConnectorTests(ITestOutputHelper output)
{
    [Fact]
    public void EvaluationOnlyFailureIsReportedBeforeBuildFinished()
    {
        using TestEnvironment env = TestEnvironment.Create(output);
        var folder = env.CreateFolder(createFolder: true);
        var project = env.CreateFile(folder, "test.proj", "<Project />");
        env.CreateFile(folder, ".editorconfig", """
            root = true

            [*.proj]
            build_check.X01237.severity = warning
            """);
        string assemblyPath = Path.Combine(AppContext.BaseDirectory, "ErrorCustomCheck.dll");
        File.Exists(assemblyPath).ShouldBeTrue();
        using var manager = new BuildManager();
        var source = new EventArgsDispatcher();
        IEventSource merged = BuildCheckReplayModeConnector.GetMergedEventSource(manager, source);
        var logger = new MockLogger(output);
        logger.Initialize(merged);
        var context = new BuildEventContext(0, 1, 21,
            BuildEventContext.InvalidProjectInstanceId, BuildEventContext.InvalidProjectContextId,
            BuildEventContext.InvalidTargetId, BuildEventContext.InvalidTaskId);

        logger.Warnings.ShouldBeEmpty();
        source.Dispatch(new BuildStartedEventArgs("started", null));
        source.Dispatch(new ProjectEvaluationStartedEventArgs { ProjectFile = project.Path, BuildEventContext = context });
        source.Dispatch(new BuildCheckAcquisitionEventArgs(assemblyPath, project.Path) { BuildEventContext = context });
        source.Dispatch(new ProjectEvaluationFinishedEventArgs { ProjectFile = project.Path, BuildEventContext = context });
        source.Dispatch(new BuildFinishedEventArgs("finished", null, true));

        BuildWarningEventArgs warning = logger.Warnings.ShouldHaveSingleItem();
        warning.Message.ShouldContain("something went wrong when executing registered action");
        warning.BuildEventContext.ShouldBeSameAs(context);
        logger.AllBuildEvents.IndexOf(warning).ShouldBeLessThan(
            logger.AllBuildEvents.FindIndex(e => e is BuildFinishedEventArgs));
    }
}
