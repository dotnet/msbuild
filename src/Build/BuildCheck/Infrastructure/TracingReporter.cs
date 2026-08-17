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
    private TimeSpan checkAcquisitionTime;
    private TimeSpan checkSetDataSourceTime;
    private TimeSpan newProjectChecksTime;

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

    public Dictionary<string, TimeSpan> GetInfrastructureTracingStats()
        => new Dictionary<string, TimeSpan>()
        {
            { $"{BuildCheckConstants.infraStatPrefix}checkAcquisitionTime", checkAcquisitionTime },
            { $"{BuildCheckConstants.infraStatPrefix}checkSetDataSourceTime", checkSetDataSourceTime },
            { $"{BuildCheckConstants.infraStatPrefix}newProjectChecksTime", newProjectChecksTime }
        };
}
