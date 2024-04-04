// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BuildCheck.Analyzers;
using Microsoft.Build.BuildCheck.Infrastructure;

namespace Microsoft.Build.BuildCheck.Acquisition;

internal class BuildCheckAcquisitionModule
{
    private static T Construct<T>() where T : new() => new();
    public BuildAnalyzerFactory CreateBuildAnalyzerFactory(AnalyzerAcquisitionData analyzerAcquisitionData)
    {
        // Acquisition module - https://github.com/orgs/dotnet/projects/373/views/1?pane=issue&itemId=52643036
        return Construct<SharedOutputPathAnalyzer>;
    }
}
