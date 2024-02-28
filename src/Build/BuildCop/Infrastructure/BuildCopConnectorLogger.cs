// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.BuildCop.Acquisition;
using Microsoft.Build.BuildCop.Logging;
using Microsoft.Build.Experimental.BuildCop;
using Microsoft.Build.Framework;

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BuildCop.Infrastructure;
internal sealed class BuildCopConnectorLogger(IBuildAnalysisLoggingContextFactory loggingContextFactory, IBuildCopManager buildCopManager)
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
                buildCopManager.ProcessEvaluationFinishedEventArgs(
                    loggingContextFactory.CreateLoggingContext(e.BuildEventContext!),
                    projectEvaluationFinishedEventArgs);
            }
            catch (Exception exception)
            {
                Debugger.Launch();
                Console.WriteLine(exception);
                throw;
            }

            buildCopManager.EndProjectEvaluation(BuildCopDataSource.EventArgs, e.BuildEventContext!);
        }
        else if (e is ProjectEvaluationStartedEventArgs projectEvaluationStartedEventArgs)
        {
            if (projectEvaluationStartedEventArgs.ProjectFile?.EndsWith(".metaproj") ?? false)
            {
                return;
            }

            buildCopManager.StartProjectEvaluation(BuildCopDataSource.EventArgs, e.BuildEventContext!,
                projectEvaluationStartedEventArgs.ProjectFile!);
        }
        else if (e is ProjectStartedEventArgs projectStartedEvent)
        {
            buildCopManager.StartProjectRequest(BuildCopDataSource.EventArgs, e.BuildEventContext!);
        }
        else if (e is ProjectFinishedEventArgs projectFinishedEventArgs)
        {
            buildCopManager.EndProjectRequest(BuildCopDataSource.EventArgs, e.BuildEventContext!);
        }
        else if (e is BuildCopEventArgs buildCopBuildEventArgs)
        {
            if (buildCopBuildEventArgs is BuildCopTracingEventArgs tracingEventArgs)
            {
                _stats.Merge(tracingEventArgs.TracingData, (span1, span2) => span1 + span2);
            }
            else if (buildCopBuildEventArgs is BuildCopAcquisitionEventArgs acquisitionEventArgs)
            {
                buildCopManager.ProcessAnalyzerAcquisition(acquisitionEventArgs.ToAnalyzerAcquisitionData());
            }
        }
    }

    private readonly Dictionary<string, TimeSpan> _stats = new Dictionary<string, TimeSpan>();

    private void EventSource_BuildFinished(object sender, BuildFinishedEventArgs e)
    {
        _stats.Merge(buildCopManager.CreateTracingStats(), (span1, span2) => span1 + span2);
        string msg = string.Join(Environment.NewLine, _stats.Select(a => a.Key + ": " + a.Value));


        BuildEventContext buildEventContext = e.BuildEventContext ?? new BuildEventContext(
            BuildEventContext.InvalidNodeId, BuildEventContext.InvalidTargetId,
            BuildEventContext.InvalidProjectContextId, BuildEventContext.InvalidTaskId);

        LoggingContext loggingContext = loggingContextFactory.CreateLoggingContext(buildEventContext).ToLoggingContext();

        // TODO: tracing: https://github.com/dotnet/msbuild/issues/9629
        loggingContext.LogCommentFromText(MessageImportance.High, msg);
    }

    public void Shutdown()
    { }
}
