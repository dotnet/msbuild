// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Analyzers.Infrastructure;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Experimental;

public class BuildCopLoggerFactory : IBuildCopLoggerFactory
{
    public ILogger CreateBuildAnalysisLogger(IBuildAnalysisLoggingContextFactory loggingContextFactory)
    {
        return new BuildCopConnectorLogger(loggingContextFactory, BuildCopManager.Instance);
    }
}
