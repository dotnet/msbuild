// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

namespace Microsoft.Build.Experimental.BuildCheck;

internal class AnalysisDispatchingContextFactory : IAnalysisContextFactory
{
    private readonly EventArgsDispatcher _eventDispatcher;

    public AnalysisDispatchingContextFactory(EventArgsDispatcher eventDispatcher) => _eventDispatcher = eventDispatcher;

    public IAnalysisContext CreateAnalysisContext(BuildEventContext eventContext)
        => new AnalysisDispatchingContext(_eventDispatcher, eventContext);
}
