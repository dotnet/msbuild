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
internal sealed class BuildCheckConnectorLogger(
    IBuildAnalysisLoggingContextFactory loggingContextFactory, 
    IBuildCheckManager buildCheckManager,
    bool isStatsEnabled)
    : ILogger
{
    public LoggerVerbosity Verbosity { get; set; }
    public string? Parameters { get; set; }

    private bool _areStatsEnabled = isStatsEnabled;

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

            buildCheckManager.StartProjectEvaluation(BuildCheckDataSource.EventArgs, e.BuildEventContext!,
                projectEvaluationStartedEventArgs.ProjectFile!);
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
                _statsAnalyzers.Merge(tracingEventArgs.TracingData, (span1, span2) => span1 + span2);
            }
            else if (buildCheckBuildEventArgs is BuildCheckAcquisitionEventArgs acquisitionEventArgs)
            {
                buildCheckManager.ProcessAnalyzerAcquisition(acquisitionEventArgs.ToAnalyzerAcquisitionData());
            }
        }
    }

    private readonly Dictionary<string, TimeSpan> _statsAnalyzers = new Dictionary<string, TimeSpan>();

    private void EventSource_BuildFinished(object sender, BuildFinishedEventArgs e)
    {

        BuildEventContext buildEventContext = e.BuildEventContext ?? new BuildEventContext(
            BuildEventContext.InvalidNodeId, BuildEventContext.InvalidTargetId,
            BuildEventContext.InvalidProjectContextId, BuildEventContext.InvalidTaskId);

        LoggingContext loggingContext = loggingContextFactory.CreateLoggingContext(buildEventContext).ToLoggingContext();

        if (_areStatsEnabled)
        {
            _statsAnalyzers.Merge(buildCheckManager.CreateAnalyzerTracingStats(), (span1, span2) => span1 + span2);
            LogAnalyzerStats(loggingContext);
        }
    }
    
    private void LogAnalyzerStats(LoggingContext loggingContext)
    {
        string infraStatPrefix = "infrastructureStat_";

        Dictionary<string, TimeSpan> infraStats = new Dictionary<string, TimeSpan>();
        Dictionary<string, TimeSpan> analyzerStats = new Dictionary<string, TimeSpan>();

        foreach (var stat in _statsAnalyzers)
        {
            if (stat.Key.StartsWith(infraStatPrefix))
            {
                string newKey = stat.Key.Replace(infraStatPrefix, string.Empty);
                infraStats[newKey] = stat.Value;
            }
            else
            {
                analyzerStats[stat.Key] = stat.Value;
            }
        }

        loggingContext.LogCommentFromText(MessageImportance.High, $"BuildCheck run times{Environment.NewLine}");
        string infraData = buildStatsTable("Infrastructure run times", infraStats);
        loggingContext.LogCommentFromText(MessageImportance.High, infraData);

        string analyzerData = buildStatsTable("Analyzer run times", analyzerStats);
        loggingContext.LogCommentFromText(MessageImportance.High, analyzerData);
    }

    private string buildStatsTable(string title, Dictionary<string, TimeSpan> rowData)
    {
        string headerSeparator = $"=============";
        string rowSeparator = $"{Environment.NewLine}----------{Environment.NewLine}";

        string header = $"{headerSeparator}{Environment.NewLine}{title}{Environment.NewLine}{headerSeparator}{Environment.NewLine}";

        string rows = string.Join(rowSeparator, rowData.Select(a => $"{a.Key} | {a.Value}"));

        return $"{header}{rows}{Environment.NewLine}";
    }

    public void Shutdown()
    { }
}
