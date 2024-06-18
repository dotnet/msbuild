// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

namespace Microsoft.Build.Experimental.BuildCheck;

internal class AnalysisDispatchingContextFactory : IAnalysisContextFactory
{
    private readonly EventArgsDispatcher _dispatcher;

    public event AnyEventHandler? AnyEventRaised;

    public AnalysisDispatchingContextFactory()
    {
        _dispatcher = new EventArgsDispatcher();

        _dispatcher.AnyEventRaised += (sender, e) => AnyEventRaised?.Invoke(sender, e);
    }

    public IAnalysisContext CreateAnalysisContext(BuildEventContext eventContext)
        => new AnalysisDispatchingContext(_dispatcher, eventContext);
}
