// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.BuildCheck.Infrastructure;

internal class TracingReporter
{
    internal Dictionary<string, TimeSpan> TracingStats { get; } = new();

    // Infrastructure time keepers, examples for now
    internal TimeSpan analyzerAcquisitionTime;
    internal long analyzerSetDataSourceTime;
    internal long newProjectAnalyzersTime;

    public void AddStats(string name, TimeSpan subtotal)
    {
        if (TracingStats.TryGetValue(name, out TimeSpan existing))
        {
            TracingStats[name] = existing + subtotal;
        }
        else
        {
            TracingStats[name] = subtotal;
        }
    }
}
