// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Experimental.BuildCheck.Acquisition;

internal interface IBuildCheckAcquisitionModule
{
    /// <summary>
    /// Creates a list of factory delegates for building analyzer rules instances from a given assembly path.
    /// </summary>
    List<BuildAnalyzerFactory> CreateBuildAnalyzerFactories(AnalyzerAcquisitionData analyzerAcquisitionData, IAnalysisContext analysisContext);
}
