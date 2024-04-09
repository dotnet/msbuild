// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BuildCheck.Infrastructure;

namespace Microsoft.Build.BuildCheck.Acquisition
{
    internal interface IBuildCheckAcquisitionModule
    {
        BuildAnalyzerFactory? CreateBuildAnalyzerFactory(AnalyzerAcquisitionData analyzerAcquisitionData);
    }
}
