// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.BuildCop.Acquisition;
using Microsoft.Build.BuildCop.Infrastructure;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Experimental.BuildCop;

internal enum BuildCopDataSource
{
    EventArgs,
    BuildExecution,

    ValuesCount = BuildExecution + 1
}

/// <summary>
/// The central manager for the BuildCop - this is the integration point with MSBuild infrastructure.
/// </summary>
internal interface IBuildCopManager
{
    void ProcessEvaluationFinishedEventArgs(
        IBuildAnalysisLoggingContext buildAnalysisContext,
        ProjectEvaluationFinishedEventArgs projectEvaluationFinishedEventArgs);

    void SetDataSource(BuildCopDataSource buildCopDataSource);

    void ProcessAnalyzerAcquisition(AnalyzerAcquisitionData acquisitionData);

    Dictionary<string, TimeSpan> CreateTracingStats();

    void FinalizeProcessing(LoggingContext loggingContext);

    // All those to be called from RequestBuilder,
    //  but as well from the ConnectorLogger - as even if interleaved, it gives the info
    //  to manager about what analyzers need to be materialized and configuration fetched.
    // No unloading of analyzers is yet considered - once loaded it stays for whole build.

    void StartProjectEvaluation(BuildCopDataSource buildCopDataSource, BuildEventContext buildEventContext, string fullPath);
    void EndProjectEvaluation(BuildCopDataSource buildCopDataSource, BuildEventContext buildEventContext);
    void StartProjectRequest(BuildCopDataSource buildCopDataSource, BuildEventContext buildEventContext);
    void EndProjectRequest(BuildCopDataSource buildCopDataSource, BuildEventContext buildEventContext);

    void Shutdown();
}
