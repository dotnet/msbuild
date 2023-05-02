// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.DotNet.Cli.Utils
{
    public interface ITelemetryFilter
    {
        IEnumerable<ApplicationInsightsEntryFormat> Filter(object o);
    }
}
