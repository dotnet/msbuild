// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

namespace Microsoft.Build.Experimental.BuildCheck;

internal class CheckDispatchingContextFactory : ICheckContextFactory
{
    private readonly EventArgsDispatcher _eventDispatcher;

    public event AnyEventHandler? AnyEventRaised;

    public CheckDispatchingContextFactory(EventArgsDispatcher eventDispatcher)
    {
        _eventDispatcher = eventDispatcher;

        _eventDispatcher.AnyEventRaised += (sender, e) => AnyEventRaised?.Invoke(sender, e);
    }

    public ICheckContext CreateCheckContext(BuildEventContext eventContext)
        => new CheckDispatchingContext(_eventDispatcher, eventContext);
}
