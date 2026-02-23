// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Reports;

namespace MSBuild.Benchmarks;

internal static class Extensions
{
    public static int ToExitCode(this IEnumerable<Summary> summaries)
    {
        // an empty summary means that initial filtering and validation did not allow to run
        if (!summaries.Any())
        {
            return 1;
        }

        // if anything has failed, it's an error
        return summaries.Any(HasAnyErrors) ? 1 : 0;
    }

    public static bool HasAnyErrors(this Summary summary)
        => summary.HasCriticalValidationErrors || summary.Reports.Any(report => report.HasAnyErrors());

    public static bool HasAnyErrors(this BenchmarkReport report)
        => !report.BuildResult.IsBuildSuccess || !report.AllMeasurements.Any();
}
