// Licensed to the .NET Foundation under one or more agreements.
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
        IAnalysisContext analysisContext,
        ProjectEvaluationFinishedEventArgs projectEvaluationFinishedEventArgs)
    {
    }

    public void SetDataSource(BuildCheckDataSource buildCheckDataSource)
    {
    }

    public void ProcessTaskStartedEventArgs(
        IAnalysisContext analysisContext,
        TaskStartedEventArgs taskStartedEventArgs)
    {
    }

    public void ProcessTaskFinishedEventArgs(
        IAnalysisContext analysisContext,
        TaskFinishedEventArgs taskFinishedEventArgs)
    {
    }

    public void ProcessTaskParameterEventArgs(
        IAnalysisContext analysisContext,
        TaskParameterEventArgs taskParameterEventArgs)
    {
    }

    public void ProcessAnalyzerAcquisition(
        AnalyzerAcquisitionData acquisitionData,
        IAnalysisContext analysisContext)
    {
    }

    public void FinalizeProcessing(LoggingContext loggingContext)
    {
    }

    public void ProjectFirstEncountered(BuildCheckDataSource buildCheckDataSource, IAnalysisContext analysisContext,
        string projectFullPath)
    {
    }

    public void StartProjectEvaluation(IAnalysisContext analysisContext, string fullPath)
    {
    }

    public void EndProjectEvaluation(BuildEventContext buildEventContext)
    {
    }

    public void StartProjectRequest(BuildEventContext buildEventContext, string projectFullPath)
    {
    }

    public void EndProjectRequest(IAnalysisContext analysisContext,
        string projectFullPath)
    {
    }

    public Dictionary<string, TimeSpan> CreateAnalyzerTracingStats() => new Dictionary<string, TimeSpan>();

    public void ProcessPropertyRead(PropertyReadInfo propertyReadInfo, AnalysisLoggingContext buildEventContext)
    { }

    public void ProcessPropertyWrite(PropertyWriteInfo propertyWriteInfo, AnalysisLoggingContext buildEventContext)
    { }

    public void ProcessEnvironmentVariableReadEventArgs(IAnalysisContext analysisContext, EnvironmentVariableReadEventArgs projectEvaluationEventArgs)
    { }
}
