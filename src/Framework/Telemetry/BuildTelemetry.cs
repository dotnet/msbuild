// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;

namespace Microsoft.Build.Framework.Telemetry
{
    /// <summary>
    /// Telemetry of build.
    /// </summary>
    internal class BuildTelemetry : TelemetryBase
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
        /// Time at which build have finished.
        /// </summary>
        public DateTime? FinishedAt { get; set; }

        /// <summary>
        /// Overall build success.
        /// </summary>
        public bool? Success { get; set; }

        /// <summary>
        /// Build Target.
        /// </summary>
        public string? Target { get; set; }

        /// <summary>
        /// MSBuild server fallback reason.
        /// Either "ServerBusy", "ConnectionError" or null (no fallback).
        /// </summary>
        public string? ServerFallbackReason { get; set; }

        /// <summary>
        /// Version of MSBuild.
        /// </summary>
        public Version? Version { get; set; }

        /// <summary>
        /// Display version of the Engine suitable for display to a user.
        /// </summary>
        public string? DisplayVersion { get; set; }

        /// <summary>
        /// Path to project file.
        /// </summary>
        public string? Project { get; set; }

        /// <summary>
        /// Host in which MSBuild build was executed.
        /// For example: "VS", "VSCode", "Azure DevOps", "GitHub Action", "CLI", ...
        /// </summary>
        public string? Host { get; set; }

        /// <summary>
        /// State of MSBuild server process before this build.
        /// One of 'cold', 'hot', null (if not run as server)
        /// </summary>
        public string? InitialServerState { get; set; }

        /// <summary>
        /// Framework name suitable for display to a user.
        /// </summary>
        public string? FrameworkName { get; set; }

        public override void UpdateEventProperties()
        {
            if (DisplayVersion != null)
            {
                Properties["BuildEngineDisplayVersion"] = DisplayVersion;
            }

            if (StartAt.HasValue && FinishedAt.HasValue)
            {
                Properties["BuildDurationInMilliseconds"] = (FinishedAt.Value - StartAt.Value).TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
            }

            if (InnerStartAt.HasValue && FinishedAt.HasValue)
            {
                Properties["InnerBuildDurationInMilliseconds"] = (FinishedAt.Value - InnerStartAt.Value).TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
            }

            if (FrameworkName != null)
            {
                Properties["BuildEngineFrameworkName"] = FrameworkName;
            }

            if (Host != null)
            {
                Properties["BuildEngineHost"] = Host;
            }

            if (InitialServerState != null)
            {
                Properties["InitialMSBuildServerState"] = InitialServerState;
            }

            if (Project != null)
            {
                Properties["ProjectPath"] = Project;
            }

            if (ServerFallbackReason != null)
            {
                Properties["ServerFallbackReason"] = ServerFallbackReason;
            }

            if (Success.HasValue)
            {
                Properties["BuildSuccess"] = Success.HasValue.ToString(CultureInfo.InvariantCulture);
            }

            if (Target != null)
            {
                Properties["BuildTarget"] = Target;
            }

            if (Version != null)
            {
                Properties["BuildEngineVersion"] = Version.ToString();
            }
        }
    }
}
