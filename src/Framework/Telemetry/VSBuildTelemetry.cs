// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK

using Microsoft.VisualStudio.Telemetry;

namespace Microsoft.Build.Framework.Telemetry
{
    internal class VSBuildTelemetry : IActivityTelemetryDataHolder
    {
        private BuildTelemetry _buildTelemetry;

        public VSBuildTelemetry(BuildTelemetry buildTelemetry) => _buildTelemetry = buildTelemetry;

        /// <summary>
        /// Create a list of properties sent to VS telemetry with the information whether they should be hashed.
        /// </summary>
        public TelemetryComplexProperty GetActivityProperties()
        {
            var buildTelemetryData = new BuildTelemetryData(
                _buildTelemetry.StartAt.HasValue && _buildTelemetry.FinishedAt.HasValue
                    ? (_buildTelemetry.FinishedAt.Value - _buildTelemetry.StartAt.Value).TotalMilliseconds.ToString()
                    : null,
                _buildTelemetry.InnerStartAt.HasValue && _buildTelemetry.FinishedAt.HasValue
                    ? (_buildTelemetry.FinishedAt.Value - _buildTelemetry.InnerStartAt.Value).TotalMilliseconds.ToString()
                    : null,
                _buildTelemetry.BuildEngineHost,
                _buildTelemetry.BuildSuccess?.ToString(),
                _buildTelemetry.BuildTarget,
                _buildTelemetry.BuildEngineVersion?.ToString(),
                _buildTelemetry.BuildCheckEnabled?.ToString(),
                _buildTelemetry.MultiThreadedModeEnabled?.ToString(),
                _buildTelemetry.SACEnabled?.ToString(),
                _buildTelemetry.IsStandaloneExecution?.ToString());

            return new TelemetryComplexProperty(buildTelemetryData);
        }

        internal readonly record struct BuildTelemetryData(
            string? BuildDuration,
            string? InnerBuildDuration,
            string? BuildEngineHost,
            string? BuildSuccess,
            string? BuildTarget,
            string? BuildEngineVersion,
            string? BuildCheckEnabled,
            string? MultiThreadedModeEnabled,
            string? SACEnabled,
            string? IsStandaloneExecution);
    }
}

#endif
