// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework.Telemetry;

namespace Microsoft.Build.Coordinator;

/// <summary>
///  Provides telemetry instrumentation for the coordinator server using MSBuild's
///  standard telemetry infrastructure (<see cref="MSBuildActivitySource"/>).
///  On .NET, activities are emitted via <see cref="System.Diagnostics.Activity"/>.
///  On .NET Framework, activities are emitted via VS Telemetry.
/// </summary>
internal static class CoordinatorTelemetry
{
    // Activity names
    private const string ActivityPrefix = "Coordinator/";

    private const string GrantActivity = $"{ActivityPrefix}Grant";
    private const string DeferredActivity = $"{ActivityPrefix}Deferred";
    private const string DeferredGrantFulfilledActivity = $"{ActivityPrefix}DeferredGrantFulfilled";
    private const string ReleasedActivity = $"{ActivityPrefix}Released";

    // Tag names
    private const string ProcessIdTag = "ProcessId";
    private const string NodesRequestedTag = "NodesRequested";
    private const string NodesGrantedTag = "NodesGranted";
    private const string NodesReleasedTag = "NodesReleased";
    private const string QueueDepthTag = "QueueDepth";
    private const string ActiveBuildsTag = "ActiveBuilds";
    private const string AllocatedNodesTag = "AllocatedNodes";

    private static IActivity? StartActivity(string name)
        => TelemetryManager.Instance.DefaultActivitySource?.StartActivity(name);

    /// <summary>
    ///  Records that a node grant was issued immediately to a build.
    /// </summary>
    public static void RecordGrantIssued(int processId, int requestedNodes, int grantedNodes, int queueDepth, int activeBuilds, int allocatedNodes)
    {
        using IActivity? _ = StartActivity(GrantActivity)
            ?.SetTag(ProcessIdTag, processId)
            ?.SetTag(NodesRequestedTag, requestedNodes)
            ?.SetTag(NodesGrantedTag, grantedNodes)
            ?.SetTag(QueueDepthTag, queueDepth)
            ?.SetTag(ActiveBuildsTag, activeBuilds)
            ?.SetTag(AllocatedNodesTag, allocatedNodes);
    }

    /// <summary>
    ///  Records that a build was queued because no nodes were available.
    /// </summary>
    public static void RecordGrantDeferred(int processId, int requestedNodes, int queueDepth)
    {
        using IActivity? _ = StartActivity(DeferredActivity)
            ?.SetTag(ProcessIdTag, processId)
            ?.SetTag(NodesRequestedTag, requestedNodes)
            ?.SetTag(QueueDepthTag, queueDepth);
    }

    /// <summary>
    ///  Records that a deferred grant was fulfilled from the wait queue.
    /// </summary>
    public static void RecordDeferredGrantFulfilled(int processId, int grantedNodes, int queueDepth, int activeBuilds, int allocatedNodes)
    {
        using IActivity? _ = StartActivity(DeferredGrantFulfilledActivity)
            ?.SetTag(ProcessIdTag, processId)
            ?.SetTag(NodesGrantedTag, grantedNodes)
            ?.SetTag(QueueDepthTag, queueDepth)
            ?.SetTag(ActiveBuildsTag, activeBuilds)
            ?.SetTag(AllocatedNodesTag, allocatedNodes);
    }

    /// <summary>
    ///  Records that a grant was released by a client.
    /// </summary>
    public static void RecordGrantReleased(int processId, int releasedNodes, int queueDepth, int activeBuilds, int allocatedNodes)
    {
        using IActivity? _ = StartActivity(ReleasedActivity)
            ?.SetTag(ProcessIdTag, processId)
            ?.SetTag(NodesReleasedTag, releasedNodes)
            ?.SetTag(QueueDepthTag, queueDepth)
            ?.SetTag(ActiveBuildsTag, activeBuilds)
            ?.SetTag(AllocatedNodesTag, allocatedNodes);
    }
}
