// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

public class BuildCheckEventArgsDispatcher : EventArgsDispatcher
{
    private readonly BuildCheckBuildEventHandler _buildCheckEventHandler;

    internal BuildCheckEventArgsDispatcher(IBuildCheckManager buildCheckManager)
        => _buildCheckEventHandler = new BuildCheckBuildEventHandler(
            new AnalysisDispatchingContextFactory(this),
            buildCheckManager);

    public override void Dispatch(BuildEventArgs buildEvent)
    {
        base.Dispatch(buildEvent);

        _buildCheckEventHandler.HandleBuildEvent(buildEvent);
    }
}
