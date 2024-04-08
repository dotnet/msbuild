// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.BuildCheck.Acquisition;
using Microsoft.Build.BuildCheck.Infrastructure;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Experimental.BuildCheck;

internal enum BuildCheckDataSource
{
    EventArgs,
    BuildExecution,

    ValuesCount = BuildExecution + 1
}

/// <summary>
/// The central manager for the BuildCheck - this is the integration point with MSBuild infrastructure.
/// </summary>
internal interface IBuildCheckManager
{
    void ProcessEvaluationFinishedEventArgs(
        IBuildAnalysisLoggingContext buildAnalysisContext,
        ProjectEvaluationFinishedEventArgs projectEvaluationFinishedEventArgs);

    void SetDataSource(BuildCheckDataSource buildCheckDataSource);

    void ProcessAnalyzerAcquisition(AnalyzerAcquisitionData acquisitionData);

    Dictionary<string, TimeSpan> CreateAnalyzerTracingStats();

    void FinalizeProcessing(LoggingContext loggingContext);

    // All those to be called from RequestBuilder,
    //  but as well from the ConnectorLogger - as even if interleaved, it gives the info
    //  to manager about what analyzers need to be materialized and configuration fetched.
    // No unloading of analyzers is yet considered - once loaded it stays for whole build.

    void StartProjectEvaluation(BuildCheckDataSource buildCheckDataSource, BuildEventContext buildEventContext, string fullPath);
    void EndProjectEvaluation(BuildCheckDataSource buildCheckDataSource, BuildEventContext buildEventContext);
    void StartProjectRequest(BuildCheckDataSource buildCheckDataSource, BuildEventContext buildEventContext);
    void EndProjectRequest(BuildCheckDataSource buildCheckDataSource, BuildEventContext buildEventContext);

    void Shutdown();
}
