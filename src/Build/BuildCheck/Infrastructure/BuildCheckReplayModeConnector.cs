// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// The class that creates an <see cref="IEventSource"/> for binary log replay with BuildCheck enabled.
/// </summary>
public static class BuildCheckReplayModeConnector
{
    /// <summary>
    /// Gets merged <see cref="IEventSource"/> for binary log replay with BuildCheck enabled.
    /// </summary>
    /// <param name="buildManager"><see cref="BuildManager"/> to get the registered <see cref="IBuildCheckManagerProvider"/> component from.</param>
    /// <param name="replayEventSource">The initial event source.</param>
    /// <returns>The merged <see cref="IEventSource"/>. Used for binary log replay.</returns>
    public static IEventSource GetMergedEventSource(
        BuildManager buildManager,
        IEventSource replayEventSource)
    {
        buildManager.EnableBuildCheck();

        var buildCheckManagerProvider = ((IBuildComponentHost)buildManager)
            .GetComponent(BuildComponentType.BuildCheckManagerProvider) as IBuildCheckManagerProvider;

        buildCheckManagerProvider!.Instance.SetDataSource(BuildCheckDataSource.EventArgs);

        var mergedEventSource = new EventArgsDispatcher();

        // Pass the events from replayEventSource to the mergedEventSource
        replayEventSource.AnyEventRaised += (sender, e) => mergedEventSource.Dispatch(e);

        // Create BuildCheckBuildEventHandler that passes new events to the mergedEventSource
        var buildCheckEventHandler = new BuildCheckBuildEventHandler(
            new AnalysisDispatchingContextFactory(mergedEventSource),
            buildCheckManagerProvider.Instance);

        // Pass the events from replayEventSource to the BuildCheckBuildEventHandler to produce new events
        replayEventSource.AnyEventRaised += (sender, e) => buildCheckEventHandler.HandleBuildEvent(e);

        return mergedEventSource;
    }
}
