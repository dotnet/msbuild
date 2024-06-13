// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

namespace Microsoft.Build.Experimental.BuildCheck;

internal class AnalysisDispatchingContextFactory : IAnalysisContextFactory
{
    private readonly Action<BuildEventArgs> _dispatch;

    public AnalysisDispatchingContextFactory(Action<BuildEventArgs> dispatch) => _dispatch = dispatch;

    public IAnalysisContext CreateAnalysisContext(BuildEventContext eventContext)
        => new AnalysisDispatchingContext(_dispatch, eventContext);
}
