// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd;

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  The current version of the coordinator protocol.
/// </summary>
internal static class Protocol
{
    /// <summary>
    ///  Protocol version. Increment when the wire format changes.
    /// </summary>
    public const byte Version = 1;

    /// <summary>
    ///  Default named pipe name base.
    /// </summary>
    public const string PipeNameBase = "msbuild-coordinator";

    /// <summary>
    ///  Default heartbeat interval in milliseconds.
    /// </summary>
    public const int DefaultHeartbeatIntervalMs = 5_000;

    /// <summary>
    ///  Default number of missed heartbeats before a grant is reclaimed.
    /// </summary>
    public const int DefaultMissedHeartbeatsThreshold = 3;

    /// <summary>
    ///  Environment variable that enables the build coordinator.
    /// </summary>
    public const string UseCoordinatorEnvironmentVariable = "MSBUILDUSECOORDINATOR";

    /// <summary>
    ///  Environment variable that overrides the heartbeat interval (in milliseconds).
    /// </summary>
    public const string HeartbeatIntervalEnvironmentVariable = "MSBUILDCOORDINATORHEARTBEAT";

    /// <summary>
    ///  Environment variable that overrides the coordinator's total node budget.
    /// </summary>
    public const string NodeBudgetEnvironmentVariable = "MSBUILDCOORDINATORNODEBUDGET";

    /// <summary>
    ///  Environment variable that overrides the coordinator's auto-shutdown timeout (in milliseconds).
    /// </summary>
    public const string ShutdownTimeoutEnvironmentVariable = "MSBUILDCOORDINATORSHUTDOWNTIMEOUT";

    /// <summary>
    ///  Gets the platform-appropriate named pipe name for the current user.
    /// </summary>
    public static string GetPipeName()
    {
        string userIdentifier = Environment.UserName;
        return NamedPipeUtil.GetPlatformSpecificPipeName($"{PipeNameBase}-{userIdentifier}");
    }
}
