// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Experimental;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging.Analyzers;

namespace Microsoft.Build.Analyzers.Infrastructure;
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
        if (e is ProjectEvaluationFinishedEventArgs projectEvaluationFinishedEventArgs &&
            !(projectEvaluationFinishedEventArgs.ProjectFile?.EndsWith(".metaproj") ?? false))
        {
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
        }
        // here handling of other event types
    }

    private void EventSource_BuildFinished(object sender, BuildFinishedEventArgs e)
    {
        BuildEventContext buildEventContext = e.BuildEventContext ?? new BuildEventContext(
            BuildEventContext.InvalidNodeId, BuildEventContext.InvalidTargetId,
            BuildEventContext.InvalidProjectContextId, BuildEventContext.InvalidTaskId);

        LoggingContext loggingContext = loggingContextFactory.CreateLoggingContext(buildEventContext).ToLoggingContext();

        // TODO: here flush the tracing stats: https://github.com/dotnet/msbuild/issues/9629
        loggingContext.LogCommentFromText(MessageImportance.High, buildCopManager.CreateTracingStats());
    }

    public void Shutdown()
    { }
}
