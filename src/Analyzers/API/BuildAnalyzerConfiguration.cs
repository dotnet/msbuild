// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Experimental;

public class BuildAnalyzerConfiguration
{
    public static BuildAnalyzerConfiguration Default { get; } = new()
    {
        LifeTimeScope = Experimental.LifeTimeScope.PerProject,
        SupportedInvocationConcurrency = InvocationConcurrency.Parallel,
        PerformanceWeightClass = Experimental.PerformanceWeightClass.Normal,
        EvaluationAnalysisScope = Experimental.EvaluationAnalysisScope.AnalyzedProjectOnly,
        Severity = BuildAnalysisResultSeverity.Info,
        IsEnabled = false,
    };

    public static BuildAnalyzerConfiguration Null { get; } = new();

    public LifeTimeScope? LifeTimeScope { get; internal init; }
    public InvocationConcurrency? SupportedInvocationConcurrency { get; internal init; }
    public PerformanceWeightClass? PerformanceWeightClass { get; internal init; }
    public EvaluationAnalysisScope? EvaluationAnalysisScope { get; internal init; }
    public BuildAnalysisResultSeverity? Severity { get; internal init; }
    public bool? IsEnabled { get; internal init; }
}
