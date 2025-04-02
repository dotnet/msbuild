﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.Build.Framework.Telemetry
{
    /// <summary>
    /// Wrapper class for ActivitySource with a <see cref="StartActivity(string)"/> method that wraps Activity name with VS OTel prefix.
    /// </summary>
    internal class MSBuildActivitySource
    {
        private readonly ActivitySource _source;
        private readonly double _sampleRate;

        public MSBuildActivitySource(string name, double sampleRate)
        {
            _source = new ActivitySource(name);
            _sampleRate = sampleRate;
        }
        /// <summary>
        /// Prefixes activity with VS OpenTelemetry.
        /// </summary>
        /// <param name="name">Name of the telemetry event without prefix.</param>
        /// <returns></returns>
        public Activity? StartActivity(string name)
        {
            var activity = Activity.Current?.HasRemoteParent == true
                ? _source.StartActivity($"{TelemetryConstants.EventPrefix}{name}", ActivityKind.Internal, parentId: Activity.Current.ParentId)
                : _source.StartActivity($"{TelemetryConstants.EventPrefix}{name}");
            activity?.WithTag(new("SampleRate", _sampleRate, false));
            return activity;
        }
    }
}
