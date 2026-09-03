// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Evaluation.Context;

namespace MSBuild.Benchmarks;

internal static class EvaluationObservationNativeBridge
{
    internal static IDisposable Enable(
        bool enabled,
        EvaluationObservationNativeMetrics? metrics)
    {
        return EvaluationObservationSession.TestOnlyConfigure(
            enabled,
            metrics is null
                ? null
                : report =>
                {
                    metrics.Reports++;
                    metrics.Observations +=
                        (report.Request is null ? 0 : 1) +
                        report.ProjectSources.Count +
                        report.PathProbes.Count +
                        report.DirectoryEnumerations.Count +
                        report.MetadataReads.Count +
                        report.FileReads.Count +
                        report.Globs.Count +
                        report.Searches.Count +
                        report.Environment.Count +
                        report.ExternalInputs.Count +
                        report.PropertyFunctions.Count +
                        report.SdkResolutions.Count +
                        report.TaskRegistrations.Count +
                        report.SideEffects.Count +
                        report.OperationFailures.Count;
                },
            retainDetails: false);
    }
}
