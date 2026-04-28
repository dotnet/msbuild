// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using static Microsoft.Build.Framework.Telemetry.BuildInsights;

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
        /// Only the file name (no directory) is emitted in telemetry to avoid leaking PII
        /// (usernames, directory structure) that are commonly embedded in full paths.
        /// </summary>
        public string? ProjectPath { get; set; }

        /// <summary>
        /// Well-known target names that are safe to emit in cleartext.
        /// Custom target names could reveal project internals and are hashed.
        /// </summary>
        private static readonly HashSet<string> KnownTargetNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Build",
            "Clean",
            "Rebuild",
            "Restore",
            "Pack",
            "Publish",
            "Test",
            "VSTest",
            "Run",
            "GetTargetFrameworks",
            "GetTargetFrameworksWithPlatformForSingleTargetFramework",
            "GetReferenceNearestTargetFrameworkTask",
            "GetTargetPath",
            "GetNativeManifest",
            "ResolveAssemblyReferences",
            "ResolveProjectReferences",
            "CoreCompile",
            "Compile",
            "PrepareForBuild",
            "GenerateBuildDependencyFile",
            "GenerateBindingRedirects",
            "GenerateRuntimeConfigurationFiles",
        };

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
        /// Error counts by category.
        /// </summary>
        public ErrorCountsInfo? ErrorCounts { get; set; }

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
            AddIfNotNull(SanitizeBuildTarget(BuildTarget), nameof(BuildTarget));
            AddIfNotNull(BuildEngineVersion);
            AddIfNotNull(BuildCheckEnabled);
            AddIfNotNull(MultiThreadedModeEnabled);
            AddIfNotNull(SACEnabled);
            AddIfNotNull(IsStandaloneExecution);
            AddIfNotNull(FailureCategory);
            AddIfNotNull(ErrorCounts);

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
            AddIfNotNull(ProjectPath != null ? Path.GetFileName(ProjectPath) : null, nameof(ProjectPath));
            AddIfNotNull(ServerFallbackReason);
            AddIfNotNull(SanitizeBuildTarget(BuildTarget), nameof(BuildTarget));
            AddIfNotNull(BuildEngineVersion?.ToString(), nameof(BuildEngineVersion));
            AddIfNotNull(BuildSuccess?.ToString(), nameof(BuildSuccess));
            AddIfNotNull(BuildCheckEnabled?.ToString(), nameof(BuildCheckEnabled));
            AddIfNotNull(MultiThreadedModeEnabled?.ToString(), nameof(MultiThreadedModeEnabled));
            AddIfNotNull(SACEnabled?.ToString(), nameof(SACEnabled));
            AddIfNotNull(IsStandaloneExecution?.ToString(), nameof(IsStandaloneExecution));
            AddIfNotNull(FailureCategory);
            AddIfNotNull(ErrorCounts?.ToString(), nameof(ErrorCounts));

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

        /// <summary>
        /// Returns the build target name if it is a well-known target, otherwise returns a SHA-256 hash.
        /// This prevents custom target names (which could reveal proprietary project details) from being
        /// sent in cleartext telemetry.
        /// BuildTarget may be a comma-separated list of target names (e.g., "Build,Clean"),
        /// so each target is sanitized individually.
        /// </summary>
        internal static string? SanitizeBuildTarget(string? buildTarget)
        {
            if (buildTarget is null)
            {
                return null;
            }

            // BuildTarget can be a comma-separated list (set via string.Join(",", targetNames)).
            // Split, sanitize each target individually, and rejoin.
            string[] targets = buildTarget.Split(',');
            for (int i = 0; i < targets.Length; i++)
            {
                string target = targets[i].Trim();
                targets[i] = KnownTargetNames.Contains(target)
                    ? target
                    : TelemetryDataUtils.GetHashed(target);
            }

            return string.Join(",", targets);
        }
    }
}
