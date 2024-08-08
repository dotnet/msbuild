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
        ICheckContext checkContext,
        ProjectEvaluationFinishedEventArgs projectEvaluationFinishedEventArgs)
    {
    }

    public void SetDataSource(BuildCheckDataSource buildCheckDataSource)
    {
    }

    public void ProcessTaskStartedEventArgs(
        ICheckContext checkContext,
        TaskStartedEventArgs taskStartedEventArgs)
    {
    }

    public void ProcessTaskFinishedEventArgs(
        ICheckContext checkContext,
        TaskFinishedEventArgs taskFinishedEventArgs)
    {
    }

    public void ProcessTaskParameterEventArgs(
        ICheckContext checkContext,
        TaskParameterEventArgs taskParameterEventArgs)
    {
    }

    public void ProcessCheckAcquisition(
        CheckAcquisitionData acquisitionData,
        ICheckContext checkContext)
    {
    }

    public void FinalizeProcessing(LoggingContext loggingContext)
    {
    }

    public void StartProjectEvaluation(BuildCheckDataSource buildCheckDataSource, ICheckContext checkContext, string fullPath)
    {
    }

    public void EndProjectEvaluation(BuildCheckDataSource buildCheckDataSource, BuildEventContext buildEventContext)
    {
    }

    public void StartProjectRequest(BuildCheckDataSource buildCheckDataSource, BuildEventContext buildEventContext, string projectFullPath)
    {
    }

    public void EndProjectRequest(BuildCheckDataSource buildCheckDataSource, ICheckContext checkContext,
        string projectFullPath)
    {
    }

    public void YieldProject(BuildCheckDataSource buildCheckDataSource, BuildEventContext buildEventContext)
    {
    }

    public void ResumeProject(BuildCheckDataSource buildCheckDataSource, BuildEventContext buildEventContext)
    {
    }

    public Dictionary<string, TimeSpan> CreateCheckTracingStats() => new Dictionary<string, TimeSpan>();

    public void StartTaskInvocation(BuildCheckDataSource buildCheckDataSource, TaskStartedEventArgs eventArgs)
    { }

    public void EndTaskInvocation(BuildCheckDataSource buildCheckDataSource, TaskFinishedEventArgs eventArgs)
    { }

    public void ProcessTaskParameter(BuildCheckDataSource buildCheckDataSource, TaskParameterEventArgs eventArg)
    { }

    public void ProcessPropertyRead(PropertyReadInfo propertyReadInfo, CheckLoggingContext buildEventContext)
    { }

    public void ProcessPropertyWrite(PropertyWriteInfo propertyWriteInfo, CheckLoggingContext buildEventContext)
    { }
	
    public void ProcessEnvironmentVariableReadEventArgs(ICheckContext checkContext, EnvironmentVariableReadEventArgs projectEvaluationEventArgs)
    { }
}
