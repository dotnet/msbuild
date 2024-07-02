// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Experimental.BuildCheck.Acquisition;
using Microsoft.Build.Experimental.BuildCheck.Utilities;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

internal sealed class BuildCheckConnectorLogger : ILogger
{
    private readonly BuildCheckBuildEventHandler _eventHandler;
    private readonly IBuildCheckManager _buildCheckManager;
    private readonly IAnalysisContextFactory _analysisContextFactory;

    internal BuildCheckConnectorLogger(
        IAnalysisContextFactory analyzerContextFactory,
        IBuildCheckManager buildCheckManager)
    {
        _buildCheckManager = buildCheckManager;
        _analysisContextFactory = analyzerContextFactory;
        _eventHandler = new BuildCheckBuildEventHandler(analyzerContextFactory, buildCheckManager);
    }

    public LoggerVerbosity Verbosity { get; set; }

    public string? Parameters { get; set; }

    public void Initialize(IEventSource eventSource)
    {
        eventSource.AnyEventRaised += EventSource_AnyEventRaised;

        if (eventSource is IEventSource3 eventSource3)
        {
            eventSource3.IncludeTaskInputs();
        }

        if (eventSource is IEventSource4 eventSource4)
        {
            eventSource4.IncludeEvaluationPropertiesAndItems();
        }
    }

    public void Shutdown()
    {
    }

    private void EventSource_AnyEventRaised(object sender, BuildEventArgs e)
        => _eventHandler.HandleBuildEvent(e);
}
