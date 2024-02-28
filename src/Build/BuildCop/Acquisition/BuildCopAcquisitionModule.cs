// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BuildCop.Analyzers;
using Microsoft.Build.BuildCop.Infrastructure;

namespace Microsoft.Build.BuildCop.Acquisition;

internal class BuildCopAcquisitionModule
{
    private static T Construct<T>() where T : new() => new();
    public BuildAnalyzerFactory CreateBuildAnalyzerFactory(AnalyzerAcquisitionData analyzerAcquisitionData)
    {
        // TODO: Acquisition module
        return Construct<SharedOutputPathAnalyzer>;
    }
}
