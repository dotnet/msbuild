// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Shared;

namespace MSBuild.Benchmarks;

internal static class EvaluationObservationNativeBridge
{
    internal static IDisposable Enable(
        bool enabled,
        EvaluationObservationNativeMetrics? metrics,
        bool collectPaths)
    {
        return EvaluationObservationSession.TestOnlyConfigure(
            enabled,
            metrics is null
                ? null
                : report =>
                {
                    metrics.Reports++;
                    metrics.PathProbes += report.PathProbes.Count;
                    metrics.Enumerations += report.DirectoryEnumerations.Count;
                    metrics.MetadataReads += report.MetadataReads.Count;
                    metrics.FileReads += report.FileReads.Count;
                    metrics.SemanticObservations +=
                        (report.Request is null ? 0 : 1) +
                        report.ProjectSources.Count +
                        report.Globs.Count +
                        report.Searches.Count +
                        report.Environment.Count +
                        report.ExternalInputs.Count +
                        report.PropertyFunctions.Count +
                        report.SdkResolutions.Count +
                        report.TaskRegistrations.Count +
                        report.SideEffects.Count +
                        report.OperationFailures.Count;
                    if (collectPaths && metrics.TryBeginPathSample())
                    {
                        foreach (EvaluationPathProbeObservation observation in report.PathProbes)
                        {
                            metrics.AddPath(observation.Path);
                        }

                        foreach (EvaluationDirectoryEnumerationObservation observation in report.DirectoryEnumerations)
                        {
                            metrics.AddEnumeration(observation);
                            metrics.AddPath(observation.Path);
                            foreach (string entry in observation.Entries)
                            {
                                metrics.AddPath(entry, observation.Path);
                            }
                        }

                        foreach (EvaluationMetadataObservation observation in report.MetadataReads)
                        {
                            metrics.AddPath(observation.Path);
                        }

                        foreach (EvaluationFileReadObservation observation in report.FileReads)
                        {
                            metrics.AddPath(observation.Path);
                        }

                        foreach (EvaluationProjectSourceObservation observation in report.ProjectSources)
                        {
                            metrics.AddPath(observation.Path);
                        }

                        foreach (EvaluationGlobObservation observation in report.Globs)
                        {
                            metrics.AddGlob(observation);
                            metrics.AddPath(observation.Directory);
                            foreach (string result in observation.Results)
                            {
                                string unescapedResult = observation.ResultsEscaped
                                    ? EscapingUtilities.UnescapeAll(result)
                                    : result;
                                if (FileMatcher.HasWildcards(unescapedResult))
                                {
                                    throw new InvalidOperationException(
                                        $"Cannot compare non-concrete glob result '{unescapedResult}' " +
                                        $"for role '{observation.Role}', root '{observation.Directory}', " +
                                        $"and include '{observation.Include}'.");
                                }

                                metrics.AddPath(
                                    unescapedResult,
                                    observation.Directory);
                            }
                        }

                        foreach (EvaluationSearchObservation observation in report.Searches)
                        {
                            foreach (string candidate in observation.Candidates)
                            {
                                metrics.AddPath(candidate);
                            }

                            foreach (string selectedPath in observation.SelectedPaths)
                            {
                                metrics.AddPath(selectedPath);
                            }
                        }

                        foreach (EvaluationSdkResolutionObservation observation in report.SdkResolutions)
                        {
                            metrics.AddPath(observation.Path);
                        }

                        foreach (EvaluationTaskRegistrationObservation observation in report.TaskRegistrations)
                        {
                            metrics.AddPath(observation.AssemblyFile);
                        }

                        foreach (EvaluationOperationFailureObservation observation in report.OperationFailures)
                        {
                            metrics.AddPath(observation.Path);
                        }
                    }
                },
            retainDetails: collectPaths);
    }
}
