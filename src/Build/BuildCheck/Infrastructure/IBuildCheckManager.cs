// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Experimental.BuildCheck.Acquisition;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// Enumerates the different data sources used in build check operations.
/// </summary>
internal enum BuildCheckDataSource
{
    /// <summary>
    /// The data source is based on event arguments.
    /// </summary>
    EventArgs,

    /// <summary>
    /// The data source is based on build execution.
    /// </summary>
    BuildExecution,

    /// <summary>
    /// Represents the total number of values in the enum, used for indexing purposes.
    /// </summary>
    ValuesCount = BuildExecution + 1,
}

/// <summary>
/// The central manager for the BuildCheck - this is the integration point with MSBuild infrastructure.
/// </summary>
internal interface IBuildCheckManager
{
    void ProcessEvaluationFinishedEventArgs(
        IAnalysisContext analysisContext,
        ProjectEvaluationFinishedEventArgs projectEvaluationFinishedEventArgs);

    void ProcessTaskStartedEventArgs(
        IAnalysisContext analysisContext,
        TaskStartedEventArgs taskStartedEventArgs);

    void ProcessTaskFinishedEventArgs(
        IAnalysisContext analysisContext,
        TaskFinishedEventArgs taskFinishedEventArgs);

    void ProcessTaskParameterEventArgs(
        IAnalysisContext analysisContext,
        TaskParameterEventArgs taskParameterEventArgs);

    void SetDataSource(BuildCheckDataSource buildCheckDataSource);

    void ProcessAnalyzerAcquisition(AnalyzerAcquisitionData acquisitionData, IAnalysisContext analysisContext);

    Dictionary<string, TimeSpan> CreateAnalyzerTracingStats();

    void FinalizeProcessing(LoggingContext loggingContext);

    // All those to be called from RequestBuilder,
    //  but as well from the ConnectorLogger - as even if interleaved, it gives the info
    //  to manager about what analyzers need to be materialized and configuration fetched.
    // No unloading of analyzers is yet considered - once loaded it stays for whole build.
    void StartProjectEvaluation(BuildCheckDataSource buildCheckDataSource, IAnalysisContext analysisContext, string fullPath);

    void EndProjectEvaluation(BuildCheckDataSource buildCheckDataSource, BuildEventContext buildEventContext);

    void StartProjectRequest(BuildCheckDataSource buildCheckDataSource, BuildEventContext buildEventContext);

    void EndProjectRequest(BuildCheckDataSource buildCheckDataSource, BuildEventContext buildEventContext);

    void Shutdown();
}
