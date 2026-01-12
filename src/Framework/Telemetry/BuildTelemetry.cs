// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Microsoft.Build.Framework.Telemetry
{
    /// <summary>
    /// Telemetry of build.
    /// </summary>
    internal class BuildTelemetry : TelemetryBase, IActivityTelemetryDataHolder
    {
        public override string EventName => "build";

        /// <summary>
        /// Time at which build have started.
        /// </summary>
        /// <remarks>
        /// It is time when build started, not when BuildManager start executing build.
        /// For example in case of MSBuild Server it is time before we connected or launched MSBuild Server.
        /// </remarks>
        public DateTime? StartAt { get; set; }

        /// <summary>
        /// Time at which inner build have started.
        /// </summary>
        /// <remarks>
        /// It is time when build internally started, i.e. when BuildManager starts it.
        /// In case of MSBuild Server it is time when Server starts build.
        /// </remarks>
        public DateTime? InnerStartAt { get; set; }

        /// <summary>
        /// True if MSBuild runs from command line.
        /// </summary>
        public bool? IsStandaloneExecution { get; set; }

        /// <summary>
        /// Time at which build have finished.
        /// </summary>
        public DateTime? FinishedAt { get; set; }

        /// <summary>
        /// Overall build success.
        /// </summary>
        public bool? BuildSuccess { get; set; }

        /// <summary>
        /// Build Target.
        /// </summary>
        public string? BuildTarget { get; set; }

        /// <summary>
        /// MSBuild server fallback reason.
        /// Either "ServerBusy", "ConnectionError" or null (no fallback).
        /// </summary>
        public string? ServerFallbackReason { get; set; }

        /// <summary>
        /// Version of MSBuild.
        /// </summary>
        public Version? BuildEngineVersion { get; set; }

        /// <summary>
        /// Display version of the Engine suitable for display to a user.
        /// </summary>
        public string? BuildEngineDisplayVersion { get; set; }

        /// <summary>
        /// Path to project file.
        /// </summary>
        public string? ProjectPath { get; set; }

        /// <summary>
        /// Host in which MSBuild build was executed.
        /// For example: "VS", "VSCode", "Azure DevOps", "GitHub Action", "CLI", ...
        /// </summary>
        public string? BuildEngineHost { get; set; }

        /// <summary>
        /// True if buildcheck was used.
        /// </summary>
        public bool? BuildCheckEnabled { get; set; }

        /// <summary>
        /// True if multithreaded mode was enabled.
        /// </summary>
        public bool? MultiThreadedModeEnabled { get; set; }

        /// <summary>
        /// True if Smart Application Control was enabled.
        /// </summary>
        public bool? SACEnabled { get; set; }

        /// <summary>
        /// State of MSBuild server process before this build.
        /// One of 'cold', 'hot', null (if not run as server)
        /// </summary>
        public string? InitialMSBuildServerState { get; set; }

        /// <summary>
        /// Framework name suitable for display to a user.
        /// </summary>
        public string? BuildEngineFrameworkName { get; set; }

        /// <summary>
        /// Primary failure category when BuildSuccess = false.
        /// One of: "Compiler", "MSBuildEngine", "Tasks", "SDKResolvers", "NETSDK", "NuGet", "BuildCheck", "Other".
        /// </summary>
        public string? FailureCategory { get; set; }

        /// <summary>
        /// Number of compiler errors (CS*, VBC*, FS*).
        /// </summary>
        public int? CompilerErrorCount { get; set; }

        /// <summary>
        /// Number of MSBuild engine errors (MSB4001-MSB4999).
        /// </summary>
        public int? MSBuildEngineErrorCount { get; set; }

        /// <summary>
        /// Number of task errors (MSB3001-MSB3999).
        /// </summary>
        public int? TaskErrorCount { get; set; }

        /// <summary>
        /// Number of SDK resolver errors (MSB4236).
        /// </summary>
        public int? SDKResolversErrorCount { get; set; }

        /// <summary>
        /// Number of .NET SDK errors (NETSDK*).
        /// </summary>
        public int? NETSDKErrorCount { get; set; }

        /// <summary>
        /// Number of NuGet errors (NU*).
        /// </summary>
        public int? NuGetErrorCount { get; set; }

        /// <summary>
        /// Number of BuildCheck errors (BC*).
        /// </summary>
        public int? BuildCheckErrorCount { get; set; }

        /// <summary>
        /// Number of other/uncategorized errors.
        /// </summary>
        public int? OtherErrorCount { get; set; }

        /// <summary>
        /// Create a list of properties sent to VS telemetry.
        /// </summary>
        public Dictionary<string, object> GetActivityProperties()
        {
            Dictionary<string, object> telemetryItems = new(8);

            if (StartAt.HasValue && FinishedAt.HasValue)
            {
                telemetryItems.Add(TelemetryConstants.BuildDurationPropertyName, (FinishedAt.Value - StartAt.Value).TotalMilliseconds);
            }

            if (InnerStartAt.HasValue && FinishedAt.HasValue)
            {
                telemetryItems.Add(TelemetryConstants.InnerBuildDurationPropertyName, (FinishedAt.Value - InnerStartAt.Value).TotalMilliseconds);
            }

            AddIfNotNull(BuildEngineHost);
            AddIfNotNull(BuildSuccess);
            AddIfNotNull(BuildTarget);
            AddIfNotNull(BuildEngineVersion);
            AddIfNotNull(BuildCheckEnabled);
            AddIfNotNull(MultiThreadedModeEnabled);
            AddIfNotNull(SACEnabled);
            AddIfNotNull(IsStandaloneExecution);
            AddIfNotNull(FailureCategory);
            AddIfNotNull(CompilerErrorCount);
            AddIfNotNull(MSBuildEngineErrorCount);
            AddIfNotNull(TaskErrorCount);
            AddIfNotNull(SDKResolversErrorCount);
            AddIfNotNull(NETSDKErrorCount);
            AddIfNotNull(NuGetErrorCount);
            AddIfNotNull(BuildCheckErrorCount);
            AddIfNotNull(OtherErrorCount);

            return telemetryItems;

            void AddIfNotNull(object? value, [CallerArgumentExpression(nameof(value))] string key = "")
            {
                if (value != null)
                {
                    telemetryItems.Add(key, value);
                }
            }
        }

        public override IDictionary<string, string> GetProperties()
        {
            var properties = new Dictionary<string, string>();

            AddIfNotNull(BuildEngineDisplayVersion);
            AddIfNotNull(BuildEngineFrameworkName);
            AddIfNotNull(BuildEngineHost);
            AddIfNotNull(InitialMSBuildServerState);
            AddIfNotNull(ProjectPath);
            AddIfNotNull(ServerFallbackReason);
            AddIfNotNull(BuildTarget);
            AddIfNotNull(BuildEngineVersion?.ToString(), nameof(BuildEngineVersion));
            AddIfNotNull(BuildSuccess?.ToString(), nameof(BuildSuccess));
            AddIfNotNull(BuildCheckEnabled?.ToString(), nameof(BuildCheckEnabled));
            AddIfNotNull(MultiThreadedModeEnabled?.ToString(), nameof(MultiThreadedModeEnabled));
            AddIfNotNull(SACEnabled?.ToString(), nameof(SACEnabled));
            AddIfNotNull(IsStandaloneExecution?.ToString(), nameof(IsStandaloneExecution));
            AddIfNotNull(FailureCategory);
            AddIfNotNull(CompilerErrorCount?.ToString(), "errorCounts.compiler");
            AddIfNotNull(MSBuildEngineErrorCount?.ToString(), "errorCounts.msbuildEngine");
            AddIfNotNull(TaskErrorCount?.ToString(), "errorCounts.tasks");
            AddIfNotNull(SDKResolversErrorCount?.ToString(), "errorCounts.sdkResolvers");
            AddIfNotNull(NETSDKErrorCount?.ToString(), "errorCounts.netsdk");
            AddIfNotNull(NuGetErrorCount?.ToString(), "errorCounts.nuget");
            AddIfNotNull(BuildCheckErrorCount?.ToString(), "errorCounts.buildCheck");
            AddIfNotNull(OtherErrorCount?.ToString(), "errorCounts.other");

            // Calculate durations
            if (StartAt.HasValue && FinishedAt.HasValue)
            {
                properties[TelemetryConstants.BuildDurationPropertyName] =
                    (FinishedAt.Value - StartAt.Value).TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
            }

            if (InnerStartAt.HasValue && FinishedAt.HasValue)
            {
                properties[TelemetryConstants.InnerBuildDurationPropertyName] =
                    (FinishedAt.Value - InnerStartAt.Value).TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
            }

            return properties;

            void AddIfNotNull(string? value, [CallerArgumentExpression(nameof(value))] string key = "")
            {
                if (value != null)
                {
                    properties[key] = value;
                }
            }
        }
    }
}
