// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
    ///  Gets the process ID of the MSBuild process that requested the grant.
    /// </summary>
    public int ProcessId { get; }

    /// <summary>
    ///  Gets the number of nodes requested by the build.
    /// </summary>
    public int RequestedNodes { get; }

    /// <summary>
    ///  Gets the number of nodes granted to the build. Zero if the build is still waiting.
    /// </summary>
    public int GrantedNodes { get; set; }

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
    public BuildGrant(Guid connectionId, int processId, int requestedNodes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestedNodes);

        ConnectionId = connectionId;
        ProcessId = processId;
        RequestedNodes = requestedNodes;
        GrantedNodes = 0;
        LastHeartbeat = DateTime.UtcNow;
    }
}
