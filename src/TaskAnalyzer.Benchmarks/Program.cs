// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

Summary[] summaries = BenchmarkSwitcher
    .FromAssembly(typeof(Program).Assembly)
    .Run(args)
    .ToArray();

if (summaries.Length == 0)
{
    return 1;
}

return summaries.Any(summary =>
    summary.HasCriticalValidationErrors ||
    summary.Reports.Any(report => !report.BuildResult.IsBuildSuccess || !report.AllMeasurements.Any()))
    ? 1
    : 0;
