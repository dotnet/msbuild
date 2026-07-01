// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework.Coordinator;

namespace Microsoft.Build.Coordinator;

/// <summary>
///  Tracks a single build's node grant.
/// </summary>
internal sealed class BuildGrant
{
    /// <summary>
    ///  Gets a unique identifier for this connection.
    /// </summary>
    public Guid ConnectionId { get; }

    /// <summary>
    ///  Gets the token that nested clients can use to join this grant.
    /// </summary>
    public Guid GrantId { get; }

    /// <summary>
    ///  Gets the process ID of the MSBuild process that requested the grant.
    /// </summary>
    public int ProcessId { get; }

    /// <summary>
    ///  Gets the number of nodes requested by the build.
    /// </summary>
    public int RequestedNodes { get; }

    /// <summary>
    ///  Gets the queue scheduling priority requested by the build.
    /// </summary>
    public CoordinatorBuildPriority Priority { get; }

    /// <summary>
    ///  Gets the number of nodes granted to the build. Zero if the build is still waiting.
    /// </summary>
    public int GrantedNodes { get; private set; }

    /// <summary>
    ///  Gets or sets the number of older lower-priority queue bypasses this build has observed.
    /// </summary>
    public int BypassCount { get; set; }

    /// <summary>
    ///  Gets a value indicating whether this grant joins another active root grant.
    /// </summary>
    public bool IsNested { get; }

    /// <summary>
    ///  Gets the time of the last heartbeat received from this build.
    /// </summary>
    public DateTime LastHeartbeat { get; set; }

    /// <summary>
    ///  Gets a value indicating whether this build has been granted nodes and is actively building.
    /// </summary>
    public bool IsActive => GrantedNodes > 0;

    /// <summary>
    ///  Creates a new build grant for the specified process.
    /// </summary>
    /// <param name="connectionId">A unique identifier for this connection.</param>
    /// <param name="processId">The OS process ID of the MSBuild process requesting nodes.</param>
    /// <param name="requestedNodes">The number of nodes the build is requesting.</param>
    /// <param name="priority">The coordinator queue scheduling priority requested by the build.</param>
    /// <param name="grantId">The root grant token to associate with this grant, or <see langword="null"/> to create a new root token.</param>
    /// <param name="isNested">Whether this grant joins another active root grant.</param>
    public BuildGrant(
        Guid connectionId,
        int processId,
        int requestedNodes,
        CoordinatorBuildPriority priority = CoordinatorBuildPriority.Normal,
        Guid? grantId = null,
        bool isNested = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestedNodes);
        ArgumentOutOfRangeException.ThrowIfLessThan((int)priority, (int)CoordinatorBuildPriority.Low, nameof(priority));
        ArgumentOutOfRangeException.ThrowIfGreaterThan((int)priority, (int)CoordinatorBuildPriority.High, nameof(priority));

        ConnectionId = connectionId;
        GrantId = grantId ?? Guid.NewGuid();
        ProcessId = processId;
        RequestedNodes = requestedNodes;
        Priority = priority;
        GrantedNodes = 0;
        BypassCount = 0;
        IsNested = isNested;
        LastHeartbeat = DateTime.UtcNow;
    }

    /// <summary>
    ///  Marks the build as active with its fixed node grant.
    /// </summary>
    public void Activate(int grantedNodes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(grantedNodes);
        if (IsActive)
        {
            throw new InvalidOperationException("Cannot change an active build grant.");
        }

        GrantedNodes = grantedNodes;
    }

    /// <summary>
    ///  Releases the fixed node grant.
    /// </summary>
    public void Release()
    {
        GrantedNodes = 0;
    }
}
