// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Coordinator;

/// <summary>
///  Tracks a single build's node grant.
/// </summary>
internal sealed class BuildGrant
{
    /// <summary>
    ///  The process ID of the MSBuild process that requested the grant.
    /// </summary>
    public int ProcessId { get; }

    /// <summary>
    ///  The number of nodes requested by the build.
    /// </summary>
    public int RequestedNodes { get; }

    /// <summary>
    ///  The number of nodes granted to the build. Zero if the build is still waiting.
    /// </summary>
    public int GrantedNodes { get; set; }

    /// <summary>
    ///  The time of the last heartbeat received from this build.
    /// </summary>
    public DateTime LastHeartbeat { get; set; }

    /// <summary>
    ///  Whether this build has been granted nodes and is actively building.
    /// </summary>
    public bool IsActive => GrantedNodes > 0;

    public BuildGrant(int processId, int requestedNodes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestedNodes);

        ProcessId = processId;
        RequestedNodes = requestedNodes;
        GrantedNodes = 0;
        LastHeartbeat = DateTime.UtcNow;
    }
}
