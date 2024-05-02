// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BuildCheck.Utilities;
using Microsoft.Build.Experimental.BuildCheck;

namespace Microsoft.Build.BuildCheck.Infrastructure;

internal class TracingReporter
{
    internal Dictionary<string, TimeSpan> TracingStats { get; } = new();

    // Infrastructure time keepers
    // TODO: add more timers throughout BuildCheck run
    private TimeSpan analyzerAcquisitionTime;
    private TimeSpan analyzerSetDataSourceTime;
    private TimeSpan newProjectAnalyzersTime;

    public void AddAnalyzerStats(string name, TimeSpan subtotal)
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

    public void AddAcquisitionStats(TimeSpan subtotal)
    {
        analyzerAcquisitionTime += subtotal;
    }

    public void AddSetDataSourceStats(TimeSpan subtotal)
    {
        analyzerSetDataSourceTime += subtotal;
    }

    public void AddNewProjectStats(TimeSpan subtotal)
    {
        newProjectAnalyzersTime += subtotal;
    }

    public void AddAnalyzerInfraStats()
    {
        var infraStats = new Dictionary<string, TimeSpan>() {
                { $"{BuildCheckConstants.infraStatPrefix}analyzerAcquisitionTime", analyzerAcquisitionTime },
                { $"{BuildCheckConstants.infraStatPrefix}analyzerSetDataSourceTime", analyzerSetDataSourceTime },
                { $"{BuildCheckConstants.infraStatPrefix}newProjectAnalyzersTime", newProjectAnalyzersTime }
            };

        TracingStats.Merge(infraStats, (span1, span2) => span1 + span2);
    }
}
