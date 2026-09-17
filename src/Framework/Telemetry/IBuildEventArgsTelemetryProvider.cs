// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Framework.Telemetry
{
    /// <summary>
    /// Implemented by exception types that can surface the simple type name of the
    /// <see cref="BuildEventArgs"/> they were processing when they failed - most notably
    /// <c>InternalLoggerException</c> in Microsoft.Build.
    /// </summary>
    /// <remarks>
    /// This lets crash telemetry (which lives in Microsoft.Build.Framework) capture the event
    /// type without taking a hard dependency on Microsoft.Build.dll and without reflecting over
    /// the exception. A plain type check (<c>is IBuildEventArgsTelemetryProvider</c>) is
    /// trimming- and Native AOT-safe, unlike the previous <c>Type.GetProperty</c> probe.
    /// </remarks>
    internal interface IBuildEventArgsTelemetryProvider
    {
        /// <summary>
        /// Gets the simple type name of the <see cref="BuildEventArgs"/> that was being delivered
        /// when the failure occurred (for example, <c>"BuildFinishedEventArgs"</c>), or
        /// <see langword="null"/> if no build event was associated with the failure.
        /// </summary>
        string? BuildEventArgsTypeName { get; }
    }
}
