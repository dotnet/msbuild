// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.BuildCheck.Infrastructure;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BuildCheck.Acquisition
{
    internal interface IBuildCheckAcquisitionModule
    {
        IEnumerable<BuildAnalyzerFactory> CreateBuildAnalyzerFactories(AnalyzerAcquisitionData analyzerAcquisitionData, BuildEventContext buildEventContext);
    }
}
