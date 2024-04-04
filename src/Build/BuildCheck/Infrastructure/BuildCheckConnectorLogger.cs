// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.BuildCheck.Acquisition;
using Microsoft.Build.BuildCheck.Logging;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BuildCheck.Infrastructure;

internal sealed class BuildCheckConnectorLogger(IBuildAnalysisLoggingContextFactory loggingContextFactory, IBuildCheckManager buildCheckManager)
    : ILogger
{
    public LoggerVerbosity Verbosity { get; set; }

    public string? Parameters { get; set; }

    public void Initialize(IEventSource eventSource)
    {
        eventSource.AnyEventRaised += EventSource_AnyEventRaised;
        eventSource.BuildFinished += EventSource_BuildFinished;
    }

    private void EventSource_AnyEventRaised(object sender, BuildEventArgs e)
    {
        if (e is ProjectEvaluationFinishedEventArgs projectEvaluationFinishedEventArgs)
        {
            if (projectEvaluationFinishedEventArgs.ProjectFile?.EndsWith(".metaproj") ?? false)
            {
                return;
            }

            try
            {
                buildCheckManager.ProcessEvaluationFinishedEventArgs(
                    loggingContextFactory.CreateLoggingContext(e.BuildEventContext!),
                    projectEvaluationFinishedEventArgs);
            }
            catch (Exception exception)
            {
                Debugger.Launch();
                Console.WriteLine(exception);
                throw;
            }

            buildCheckManager.EndProjectEvaluation(BuildCheckDataSource.EventArgs, e.BuildEventContext!);
        }
        else if (e is ProjectEvaluationStartedEventArgs projectEvaluationStartedEventArgs)
        {
            if (projectEvaluationStartedEventArgs.ProjectFile?.EndsWith(".metaproj") ?? false)
            {
                return;
            }

            buildCheckManager.StartProjectEvaluation(BuildCheckDataSource.EventArgs, e.BuildEventContext!, projectEvaluationStartedEventArgs.ProjectFile!);
        }
        else if (e is ProjectStartedEventArgs projectStartedEvent)
        {
            buildCheckManager.StartProjectRequest(BuildCheckDataSource.EventArgs, e.BuildEventContext!);
        }
        else if (e is ProjectFinishedEventArgs projectFinishedEventArgs)
        {
            buildCheckManager.EndProjectRequest(BuildCheckDataSource.EventArgs, e.BuildEventContext!);
        }
        else if (e is BuildCheckEventArgs buildCheckBuildEventArgs)
        {
            if (buildCheckBuildEventArgs is BuildCheckTracingEventArgs tracingEventArgs)
            {
                _stats.Merge(tracingEventArgs.TracingData, (span1, span2) => span1 + span2);
            }
            else if (buildCheckBuildEventArgs is BuildCheckAcquisitionEventArgs acquisitionEventArgs)
            {
                buildCheckManager.ProcessAnalyzerAcquisition(acquisitionEventArgs.ToAnalyzerAcquisitionData());
            }
        }
    }

    private readonly Dictionary<string, TimeSpan> _stats = new Dictionary<string, TimeSpan>();

    private void EventSource_BuildFinished(object sender, BuildFinishedEventArgs e)
    {
        _stats.Merge(buildCheckManager.CreateTracingStats(), (span1, span2) => span1 + span2);
        string msg = string.Join(Environment.NewLine, _stats.Select(a => a.Key + ": " + a.Value));


        BuildEventContext buildEventContext = e.BuildEventContext ?? new BuildEventContext(
            BuildEventContext.InvalidNodeId, BuildEventContext.InvalidTargetId,
            BuildEventContext.InvalidProjectContextId, BuildEventContext.InvalidTaskId);

        LoggingContext loggingContext = loggingContextFactory.CreateLoggingContext(buildEventContext);

        // Tracing: https://github.com/dotnet/msbuild/issues/9629
        loggingContext.LogCommentFromText(MessageImportance.High, msg);
    }

    public void Shutdown()
    { }
}
