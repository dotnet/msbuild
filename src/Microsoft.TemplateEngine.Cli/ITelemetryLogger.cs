// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Cli
{
    public interface ITelemetryLogger
    {
        void TrackEvent(string eventName, IDictionary<string, string> properties = null, IDictionary<string, double> measurements = null);
    }
}
