// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.BuildCop.Acquisition;
using Microsoft.Build.Experimental.BuildCop;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BuildCop.Infrastructure;

internal class NullBuildCopManager : IBuildCopManager
{
    public void Shutdown() { }

    public void ProcessEvaluationFinishedEventArgs(IBuildAnalysisLoggingContext buildAnalysisContext,
        ProjectEvaluationFinishedEventArgs projectEvaluationFinishedEventArgs)
    { }

    public void SetDataSource(BuildCopDataSource buildCopDataSource) { }
    public void ProcessAnalyzerAcquisition(AnalyzerAcquisitionData acquisitionData) { }

    public Dictionary<string, TimeSpan> CreateTracingStats() => throw new NotImplementedException();

    public void FinalizeProcessing(LoggingContext loggingContext)
    { }

    public void StartProjectEvaluation(BuildCopDataSource buildCopDataSource, BuildEventContext buildEventContext,
        string fullPath)
    { }

    public void EndProjectEvaluation(BuildCopDataSource buildCopDataSource, BuildEventContext buildEventContext)
    { }

    public void StartProjectRequest(BuildCopDataSource buildCopDataSource, BuildEventContext buildEventContext)
    { }

    public void EndProjectRequest(BuildCopDataSource buildCopDataSource, BuildEventContext buildEventContext)
    { }

    public void YieldProject(BuildCopDataSource buildCopDataSource, BuildEventContext buildEventContext)
    { }

    public void ResumeProject(BuildCopDataSource buildCopDataSource, BuildEventContext buildEventContext)
    { }
}
