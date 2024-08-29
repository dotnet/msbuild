// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Experimental.BuildCheck.Utilities;
using Microsoft.Build.Experimental.BuildCheck;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

internal class TracingReporter
{
    internal Dictionary<string, TimeSpan> TracingStats { get; } = new();

    // Infrastructure time keepers
    // TODO: add more timers throughout BuildCheck run
    private TimeSpan checkAcquisitionTime;
    private TimeSpan checkSetDataSourceTime;
    private TimeSpan newProjectChecksTime;

    public void AddCheckStats(string name, TimeSpan subtotal)
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
        checkAcquisitionTime += subtotal;
    }

    public void AddSetDataSourceStats(TimeSpan subtotal)
    {
        checkSetDataSourceTime += subtotal;
    }

    public void AddNewProjectStats(TimeSpan subtotal)
    {
        newProjectChecksTime += subtotal;
    }

    public void AddCheckInfraStats()
    {
        var infraStats = new Dictionary<string, TimeSpan>() {
                { $"{BuildCheckConstants.infraStatPrefix}checkAcquisitionTime", checkAcquisitionTime },
                { $"{BuildCheckConstants.infraStatPrefix}checkSetDataSourceTime", checkSetDataSourceTime },
                { $"{BuildCheckConstants.infraStatPrefix}newProjectChecksTime", newProjectChecksTime }
            };

        TracingStats.Merge(infraStats, (span1, span2) => span1 + span2);
    }
}
