// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK

using System.Diagnostics;
using Microsoft.VisualStudio.Telemetry;

namespace Microsoft.Build.Framework.Telemetry;

/// <summary>
/// Interface for classes that hold telemetry data that should be added as tags to an <see cref="Activity"/>.
/// </summary>
internal interface IActivityTelemetryDataHolder
{
    TelemetryComplexProperty GetActivityProperties();
}

#endif
