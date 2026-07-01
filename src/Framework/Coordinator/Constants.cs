// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  Constants for the build coordinator, including environment variable names
///  used for configuration.
/// </summary>
internal static class Constants
{
    /// <summary>
    ///  Name of environment variable that overrides coordinator heartbeat interval in milliseconds.
    /// </summary>
    public const string HeartbeatIntervalEnvVarName = "MSBUILDCOORDINATORHEARTBEAT";

    /// <summary>
    ///  Name of environment variable that overrides coordinator total node budget.
    /// </summary>
    public const string NodeBudgetEnvVarName = "MSBUILDCOORDINATORNODEBUDGET";

    /// <summary>
    ///  Name of environment variable that overrides coordinator auto-shutdown timeout in milliseconds.
    /// </summary>
    public const string ShutdownTimeoutEnvVarName = "MSBUILDCOORDINATORSHUTDOWNTIMEOUT";

    /// <summary>
    ///  Name of environment variable that overrides coordinator pipe name.
    /// </summary>
    public const string PipeNameEnvVarName = "MSBUILDCOORDINATORPIPENAME";

    /// <summary>
    ///  Name of environment variable used to flow an active coordinator grant token to child processes.
    /// </summary>
    public const string GrantIdEnvVarName = "MSBUILDCOORDINATORGRANTID";

    /// <summary>
    ///  Name of environment variable that overrides nodes reserved for high-priority builds.
    /// </summary>
    public const string HighPriorityReservedNodesEnvVarName = "MSBUILDCOORDINATORHIGHPRIORITYRESERVEDNODES";

    /// <summary>
    ///  Name of environment variable that overrides the maximum nodes granted to one build.
    /// </summary>
    public const string MaxNodesPerBuildEnvVarName = "MSBUILDCOORDINATORMAXNODESPERBUILD";

    /// <summary>
    ///  Name of environment variable that requests a coordinator queue priority for this build.
    /// </summary>
    public const string BuildRequestPriorityEnvVarName = "MSBUILDCOORDINATORBUILDREQUESTPRIORITY";

    /// <summary>
    ///  Name of environment variable that overrides the number of bypasses required before queued builds age upward.
    /// </summary>
    public const string PriorityAgingThresholdEnvVarName = "MSBUILDCOORDINATORPRIORITYAGINGTHRESHOLD";
}
