// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Experimental.BuildCheck;

namespace Microsoft.Build.Experimental.BuildCheck.Acquisition;

// https://github.com/dotnet/msbuild/issues/9633
// Acquisition
//  define the data that will be passed to the acquisition module (and remoted if needed)
internal class CheckAcquisitionData(string assemblyPath, string projectPath)
{
    public string AssemblyPath { get; init; } = assemblyPath;

    public string ProjectPath { get; init; } = projectPath;
}

internal static class CheckAcquisitionDataExtensions
{
    public static CheckAcquisitionData ToCheckAcquisitionData(this BuildCheckAcquisitionEventArgs eventArgs) =>
        new(eventArgs.AcquisitionPath, eventArgs.ProjectPath);

    public static BuildCheckAcquisitionEventArgs ToBuildEventArgs(this CheckAcquisitionData data) => new(data.AssemblyPath, data.ProjectPath);
}
