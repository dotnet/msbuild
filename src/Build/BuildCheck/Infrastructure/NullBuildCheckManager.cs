// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.BuildCheck.Acquisition;
using Microsoft.Build.BuildCheck.Logging;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BuildCheck.Infrastructure;

internal class NullBuildCheckManager : IBuildCheckManager, IBuildEngineDataConsumer
{
    public void Shutdown()
    {
    }

    public void ProcessAnalyzerAcquisition(AnalyzerAcquisitionData acquisitionData) { }

    public void ProcessEvaluationFinishedEventArgs(AnalyzerLoggingContext buildAnalysisContext,
        ProjectEvaluationFinishedEventArgs projectEvaluationFinishedEventArgs)
    {
    }

    public void SetDataSource(BuildCheckDataSource buildCheckDataSource)
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

    public Dictionary<string, TimeSpan> CreateAnalyzerTracingStats() => new Dictionary<string, TimeSpan>();

    public void EndProjectRequest(BuildCheckDataSource buildCheckDataSource, BuildEventContext buildEventContext, string fullPath)
    { }

    public void ProcessPropertyRead(string propertyName, int startIndex, int endIndex, IMsBuildElementLocation elementLocation,
        bool isUninitialized, PropertyReadContext propertyReadContext, BuildEventContext? buildEventContext)
    { }

    public void ProcessPropertyWrite(string propertyName, bool isEmpty, IMsBuildElementLocation? elementLocation, BuildEventContext? buildEventContext)
    { }
}
