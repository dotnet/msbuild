﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Experimental.BuildCheck.Acquisition;
using Microsoft.Build.Experimental.BuildCheck.Logging;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

internal class NullBuildCheckManager : IBuildCheckManager
{
    public void Shutdown()
    {
    }

    public void ProcessEvaluationFinishedEventArgs(
        AnalyzerLoggingContext buildAnalysisContext,
        ProjectEvaluationFinishedEventArgs projectEvaluationFinishedEventArgs)
    {
    }

    public void SetDataSource(BuildCheckDataSource buildCheckDataSource)
    {
    }

    public void ProcessTaskStartedEventArgs(AnalyzerLoggingContext buildAnalysisContext,
        TaskStartedEventArgs taskStartedEventArgs)
    {
    }

    public void ProcessTaskFinishedEventArgs(AnalyzerLoggingContext buildAnalysisContext,
        TaskFinishedEventArgs taskFinishedEventArgs)
    {
    }

    public void ProcessTaskParameterEventArgs(AnalyzerLoggingContext buildAnalysisContext,
        TaskParameterEventArgs taskParameterEventArgs)
    {
    }

    public void ProcessAnalyzerAcquisition(AnalyzerAcquisitionData acquisitionData, BuildEventContext buildEventContext) 
    {
    }

    public void FinalizeProcessing(LoggingContext loggingContext)
    {
    }

    public void StartProjectEvaluation(BuildCheckDataSource buildCheckDataSource, BuildEventContext buildEventContext, string fullPath)
    {
    }

    public void EndProjectEvaluation(BuildCheckDataSource buildCheckDataSource, BuildEventContext buildEventContext)
    {
    }

    public void StartProjectRequest(BuildCheckDataSource buildCheckDataSource, BuildEventContext buildEventContext)
    {
    }

    public void EndProjectRequest(BuildCheckDataSource buildCheckDataSource, BuildEventContext buildEventContext)
    {
    }

    public void YieldProject(BuildCheckDataSource buildCheckDataSource, BuildEventContext buildEventContext)
    {
    }

    public void ResumeProject(BuildCheckDataSource buildCheckDataSource, BuildEventContext buildEventContext)
    {
    }

    public Dictionary<string, TimeSpan> CreateAnalyzerTracingStats() => new Dictionary<string, TimeSpan>();

    public void StartTaskInvocation(BuildCheckDataSource buildCheckDataSource, TaskStartedEventArgs eventArgs)
    { }

    public void EndTaskInvocation(BuildCheckDataSource buildCheckDataSource, TaskFinishedEventArgs eventArgs)
    { }

    public void ProcessTaskParameter(BuildCheckDataSource buildCheckDataSource, TaskParameterEventArgs eventArg)
    { }
}
