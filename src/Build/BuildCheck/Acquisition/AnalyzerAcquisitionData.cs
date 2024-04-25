// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Experimental.BuildCheck;

namespace Microsoft.Build.BuildCheck.Acquisition;

// https://github.com/dotnet/msbuild/issues/9633
// Acquisition
//  define the data that will be passed to the acquisition module (and remoted if needed)
internal class AnalyzerAcquisitionData(string assemblyPath)
{
    public string AssemblyPath { get; init; } = assemblyPath;
}

internal static class AnalyzerAcquisitionDataExtensions
{
    public static AnalyzerAcquisitionData ToAnalyzerAcquisitionData(this BuildCheckAcquisitionEventArgs eventArgs) =>
        new(eventArgs.AcquisitionPath);

    public static BuildCheckAcquisitionEventArgs ToBuildEventArgs(this AnalyzerAcquisitionData data) => new(data.AssemblyPath);
}
