// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
    ///  Environment variable that overrides the coordinator pipe name.
    ///  When set, both the coordinator process and MSBuild clients will use this
    ///  name instead of the default user-scoped pipe name.
    /// </summary>
    public const string PipeNameEnvironmentVariable = "MSBUILDCOORDINATORPIPENAME";
}
