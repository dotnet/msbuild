// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

using System.Diagnostics;
using Microsoft.Build.Framework.Telemetry;
using Microsoft.Build.UnitTests;
using Shouldly;

namespace Microsoft.Build.Coordinator.UnitTests;

[TestClass]
public class CoordinatorTelemetry_Tests(TestContext outputHelper)
{
    [MSBuildTestMethod]
    public void RecordGrantIssued_EmitsActivityWithExpectedTags()
    {
        using TestEnvironment env = TestEnvironment.Create(outputHelper);
        env.SetEnvironmentVariable("MSBUILD_TELEMETRY_OPTOUT", null);
        env.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", null);

        List<Activity> captured = [];
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.StartsWith(TelemetryConstants.ActivitySourceNamespacePrefix, StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => { lock (captured) { captured.Add(a); } },
        };

        ActivitySource.AddActivityListener(listener);
        TelemetryManager.ResetForTest();
        TelemetryManager.Instance.Initialize(isStandalone: true);

        var connectionId = Guid.NewGuid();
        var grant = new BuildGrant(connectionId, processId: 42, requestedNodes: 8) { GrantedNodes = 4 };
        CoordinatorTelemetry.RecordGrantIssued(
            grant,
            queueDepth: 1,
            activeBuilds: 2,
            allocatedNodes: 12);

        Activity activity = captured.ShouldHaveSingleItem();
        activity.DisplayName.ShouldBe("VS/MSBuild/Coordinator/Grant");
        activity.GetTagItem("VS.MSBuild.ConnectionId").ShouldBe(connectionId);
        activity.GetTagItem("VS.MSBuild.ProcessId").ShouldBe(42);
        activity.GetTagItem("VS.MSBuild.NodesRequested").ShouldBe(8);
        activity.GetTagItem("VS.MSBuild.NodesGranted").ShouldBe(4);
        activity.GetTagItem("VS.MSBuild.QueueDepth").ShouldBe(1);
        activity.GetTagItem("VS.MSBuild.ActiveBuilds").ShouldBe(2);
        activity.GetTagItem("VS.MSBuild.AllocatedNodes").ShouldBe(12);
    }

    [MSBuildTestMethod]
    public void RecordGrantDeferred_EmitsActivityWithExpectedTags()
    {
        using TestEnvironment env = TestEnvironment.Create(outputHelper);
        env.SetEnvironmentVariable("MSBUILD_TELEMETRY_OPTOUT", null);
        env.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", null);

        List<Activity> captured = [];
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.StartsWith(TelemetryConstants.ActivitySourceNamespacePrefix, StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => { lock (captured) { captured.Add(a); } },
        };

        ActivitySource.AddActivityListener(listener);
        TelemetryManager.ResetForTest();
        TelemetryManager.Instance.Initialize(isStandalone: true);

        var connectionId = Guid.NewGuid();
        var grant = new BuildGrant(connectionId, processId: 99, requestedNodes: 16);
        CoordinatorTelemetry.RecordGrantDeferred(
            grant,
            queueDepth: 3);

        Activity activity = captured.ShouldHaveSingleItem();
        activity.DisplayName.ShouldBe("VS/MSBuild/Coordinator/Deferred");
        activity.GetTagItem("VS.MSBuild.ConnectionId").ShouldBe(connectionId);
        activity.GetTagItem("VS.MSBuild.ProcessId").ShouldBe(99);
        activity.GetTagItem("VS.MSBuild.NodesRequested").ShouldBe(16);
        activity.GetTagItem("VS.MSBuild.QueueDepth").ShouldBe(3);
    }

    [MSBuildTestMethod]
    public void RecordGrantReleased_EmitsActivityWithExpectedTags()
    {
        using TestEnvironment env = TestEnvironment.Create(outputHelper);
        env.SetEnvironmentVariable("MSBUILD_TELEMETRY_OPTOUT", null);
        env.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", null);

        List<Activity> captured = [];
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.StartsWith(TelemetryConstants.ActivitySourceNamespacePrefix, StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => { lock (captured) { captured.Add(a); } },
        };

        ActivitySource.AddActivityListener(listener);
        TelemetryManager.ResetForTest();
        TelemetryManager.Instance.Initialize(isStandalone: true);

        var connectionId = Guid.NewGuid();
        var grant = new BuildGrant(connectionId, processId: 7, requestedNodes: 4) { GrantedNodes = 4 };
        CoordinatorTelemetry.RecordGrantReleased(
            grant,
            queueDepth: 0,
            activeBuilds: 1,
            allocatedNodes: 8);

        Activity activity = captured.ShouldHaveSingleItem();
        activity.DisplayName.ShouldBe("VS/MSBuild/Coordinator/Released");
        activity.GetTagItem("VS.MSBuild.ConnectionId").ShouldBe(connectionId);
        activity.GetTagItem("VS.MSBuild.ProcessId").ShouldBe(7);
        activity.GetTagItem("VS.MSBuild.NodesReleased").ShouldBe(4);
        activity.GetTagItem("VS.MSBuild.QueueDepth").ShouldBe(0);
        activity.GetTagItem("VS.MSBuild.ActiveBuilds").ShouldBe(1);
        activity.GetTagItem("VS.MSBuild.AllocatedNodes").ShouldBe(8);
    }
}

#endif
