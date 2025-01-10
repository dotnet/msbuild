// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;


namespace Microsoft.Build.Framework.Telemetry
{
    internal class MSBuildActivitySource
    {
        private readonly ActivitySource _source;

        public MSBuildActivitySource(string name)
        {
            _source = new ActivitySource(name);
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
            return activity;
        }
    }
}
