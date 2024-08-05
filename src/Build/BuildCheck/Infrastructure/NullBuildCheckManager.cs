﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Experimental.BuildCheck.Acquisition;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

internal class NullBuildCheckManager : IBuildCheckManager, IBuildEngineDataRouter
{
    public void Shutdown()
    {
    }

    public void ProcessEvaluationFinishedEventArgs(
        ICheckContext analysisContext,
        ProjectEvaluationFinishedEventArgs projectEvaluationFinishedEventArgs)
    {
    }

    public void SetDataSource(BuildCheckDataSource buildCheckDataSource)
    {
    }

    public void ProcessTaskStartedEventArgs(
        ICheckContext analysisContext,
        TaskStartedEventArgs taskStartedEventArgs)
    {
    }

    public void ProcessTaskFinishedEventArgs(
        ICheckContext analysisContext,
        TaskFinishedEventArgs taskFinishedEventArgs)
    {
    }

    public void ProcessTaskParameterEventArgs(
        ICheckContext analysisContext,
        TaskParameterEventArgs taskParameterEventArgs)
    {
    }

    public void ProcessAnalyzerAcquisition(
        CheckAcquisitionData acquisitionData,
        ICheckContext analysisContext)
    {
    }

    public void FinalizeProcessing(LoggingContext loggingContext)
    {
    }

    public void StartProjectEvaluation(BuildCheckDataSource buildCheckDataSource, ICheckContext analysisContext, string fullPath)
    {
    }

    public void EndProjectEvaluation(BuildCheckDataSource buildCheckDataSource, BuildEventContext buildEventContext)
    {
    }

    public void StartProjectRequest(BuildCheckDataSource buildCheckDataSource, BuildEventContext buildEventContext, string projectFullPath)
    {
    }

    public void EndProjectRequest(BuildCheckDataSource buildCheckDataSource, ICheckContext analysisContext,
        string projectFullPath)
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

    public void ProcessPropertyRead(PropertyReadInfo propertyReadInfo, AnalysisLoggingContext buildEventContext)
    { }

    public void ProcessPropertyWrite(PropertyWriteInfo propertyWriteInfo, AnalysisLoggingContext buildEventContext)
    { }
	
    public void ProcessEnvironmentVariableReadEventArgs(ICheckContext analysisContext, EnvironmentVariableReadEventArgs projectEvaluationEventArgs)
    { }
}
