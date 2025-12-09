// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Build.Framework.Telemetry;

/// <summary>
/// Interface for classes that hold telemetry data that should be added as tags to an <see cref="IActivity"/>.
/// </summary>
internal interface IActivityTelemetryDataHolder
{
    Dictionary<string, object> GetActivityProperties();
}
