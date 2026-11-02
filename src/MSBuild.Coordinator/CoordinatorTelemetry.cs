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
    private const string ConnectionIdTag = "ConnectionId";
    private const string ProcessIdTag = "ProcessId";
    private const string NodesRequestedTag = "NodesRequested";
    private const string NodesGrantedTag = "NodesGranted";
    private const string NodesReleasedTag = "NodesReleased";
    private const string QueueDepthTag = "QueueDepth";
    private const string ActiveBuildsTag = "ActiveBuilds";
    private const string AllocatedNodesTag = "AllocatedNodes";
    private const string GrantIdTag = "GrantId";
    private const string IsNestedTag = "IsNested";

    private static IActivity? StartActivity(string name)
        => TelemetryManager.Instance.DefaultActivitySource?.StartActivity(name);

    /// <summary>
    ///  Records that a node grant was issued immediately to a build.
    /// </summary>
    public static void RecordGrantIssued(BuildGrant grant, int queueDepth, int activeBuilds, int allocatedNodes)
    {
        using IActivity? _ = StartActivity(GrantActivity)
            ?.SetTag(ConnectionIdTag, grant.ConnectionId)
            .SetTag(GrantIdTag, grant.GrantId)
            .SetTag(IsNestedTag, grant.IsNested)
            .SetTag(ProcessIdTag, grant.ProcessId)
            .SetTag(NodesRequestedTag, grant.RequestedNodes)
            .SetTag(NodesGrantedTag, grant.GrantedNodes)
            .SetTag(QueueDepthTag, queueDepth)
            .SetTag(ActiveBuildsTag, activeBuilds)
            .SetTag(AllocatedNodesTag, allocatedNodes);
    }

    /// <summary>
    ///  Records that a build was queued because no nodes were available.
    /// </summary>
    public static void RecordGrantDeferred(BuildGrant grant, int queueDepth)
    {
        using IActivity? _ = StartActivity(DeferredActivity)
            ?.SetTag(ConnectionIdTag, grant.ConnectionId)
            .SetTag(GrantIdTag, grant.GrantId)
            .SetTag(IsNestedTag, grant.IsNested)
            .SetTag(ProcessIdTag, grant.ProcessId)
            .SetTag(NodesRequestedTag, grant.RequestedNodes)
            .SetTag(QueueDepthTag, queueDepth);
    }

    /// <summary>
    ///  Records that a deferred grant was fulfilled from the wait queue.
    /// </summary>
    public static void RecordDeferredGrantFulfilled(BuildGrant grant, int queueDepth, int activeBuilds, int allocatedNodes)
    {
        using IActivity? _ = StartActivity(DeferredGrantFulfilledActivity)
            ?.SetTag(ConnectionIdTag, grant.ConnectionId)
            .SetTag(GrantIdTag, grant.GrantId)
            .SetTag(IsNestedTag, grant.IsNested)
            .SetTag(ProcessIdTag, grant.ProcessId)
            .SetTag(NodesGrantedTag, grant.GrantedNodes)
            .SetTag(QueueDepthTag, queueDepth)
            .SetTag(ActiveBuildsTag, activeBuilds)
            .SetTag(AllocatedNodesTag, allocatedNodes);
    }

    /// <summary>
    ///  Records that a grant was released by a client.
    /// </summary>
    public static void RecordGrantReleased(BuildGrant grant, int queueDepth, int activeBuilds, int allocatedNodes)
    {
        using IActivity? _ = StartActivity(ReleasedActivity)
            ?.SetTag(ConnectionIdTag, grant.ConnectionId)
            .SetTag(GrantIdTag, grant.GrantId)
            .SetTag(IsNestedTag, grant.IsNested)
            .SetTag(ProcessIdTag, grant.ProcessId)
            .SetTag(NodesReleasedTag, grant.GrantedNodes)
            .SetTag(QueueDepthTag, queueDepth)
            .SetTag(ActiveBuildsTag, activeBuilds)
            .SetTag(AllocatedNodesTag, allocatedNodes);
    }
}
