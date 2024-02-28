// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Experimental.BuildCop;

namespace Microsoft.Build.BuildCop.Acquisition;

// TODO: Acquisition
//  define the data that will be passed to the acquisition module (and remoted if needed)
internal class AnalyzerAcquisitionData(string data)
{
    public string Data { get; init; } = data;
}

internal static class AnalyzerAcquisitionDataExtensions
{
    public static AnalyzerAcquisitionData ToAnalyzerAcquisitionData(this BuildCopAcquisitionEventArgs eventArgs) =>
        new(eventArgs.AcquisitionData);

    public static BuildCopAcquisitionEventArgs ToBuildEventArgs(this AnalyzerAcquisitionData data) => new(data.Data);
}
