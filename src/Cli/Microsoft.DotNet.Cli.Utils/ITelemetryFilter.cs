// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils
{
    public interface ITelemetryFilter
    {
        IEnumerable<ApplicationInsightsEntryFormat> Filter(object o);
    }
}
