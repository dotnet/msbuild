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
        ICheckContext checksContext,
        ProjectEvaluationFinishedEventArgs projectEvaluationFinishedEventArgs);

    void ProcessEnvironmentVariableReadEventArgs(
        ICheckContext checksContext,
        EnvironmentVariableReadEventArgs envVariableReadEventArgs);

    void ProcessTaskStartedEventArgs(
        ICheckContext checksContext,
        TaskStartedEventArgs taskStartedEventArgs);

    void ProcessTaskFinishedEventArgs(
        ICheckContext checksContext,
        TaskFinishedEventArgs taskFinishedEventArgs);

    void ProcessTaskParameterEventArgs(
        ICheckContext checksContext,
        TaskParameterEventArgs taskParameterEventArgs);

    void ProcessBuildFinished(ICheckContext analysisContext);

    void SetDataSource(BuildCheckDataSource buildCheckDataSource);

    void ProcessCheckAcquisition(CheckAcquisitionData acquisitionData, ICheckContext checksContext);

    Dictionary<string, TimeSpan> CreateCheckTracingStats();

    void FinalizeProcessing(LoggingContext loggingContext);

    // All those to be called from RequestBuilder,
    //  but as well from the ConnectorLogger - as even if interleaved, it gives the info
    //  to manager about what checks need to be materialized and configuration fetched.
    // No unloading of checks is yet considered - once loaded it stays for whole build.
    
	
    // Project might be encountered first time in some node, but be already evaluated in another - so StartProjectEvaluation won't happen
    //  - but we still need to know about it, hence the dedicated event.
    void ProjectFirstEncountered(BuildCheckDataSource buildCheckDataSource, ICheckContext analysisContext, string projectFullPath);

    void ProcessProjectEvaluationStarted(ICheckContext checksContext, string projectFullPath);

    void EndProjectEvaluation(BuildEventContext buildEventContext);

    void StartProjectRequest(BuildEventContext buildEventContext, string projectFullPath);

    void EndProjectRequest(ICheckContext checksContext, string projectFullPath);

    void Shutdown();
}
