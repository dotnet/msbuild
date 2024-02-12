// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Experimental;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Analyzers.Infrastructure;
internal sealed class AnalyzersConnectorLogger(IBuildAnalysisLoggingContextFactory loggingContextFactory, IBuildAnalysisManager buildAnalysisManager)
    : ILogger
{
    public LoggerVerbosity Verbosity { get; set; }
    public string? Parameters { get; set; }

    public void Initialize(IEventSource eventSource)
    {
        eventSource.AnyEventRaised += EventSource_AnyEventRaised;
    }

    private void EventSource_AnyEventRaised(object sender, BuildEventArgs e)
    {
        if (e is ProjectEvaluationFinishedEventArgs projectEvaluationFinishedEventArgs &&
            !(projectEvaluationFinishedEventArgs.ProjectFile?.EndsWith(".metaproj") ?? false))
        {
            // Debugger.Launch();

            try
            {
                buildAnalysisManager.ProcessEvaluationFinishedEventArgs(
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

    public void Shutdown()
    {
        // TODO: here flush the tracing stats: https://github.com/dotnet/msbuild/issues/9629
    }
}
