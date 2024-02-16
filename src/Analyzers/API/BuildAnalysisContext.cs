// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Analyzers.Infrastructure;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Experimental;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Experimental;

public class BuildAnalysisContext
{
    private protected readonly LoggingContext _loggingContext;

    internal BuildAnalysisContext(LoggingContext loggingContext) => _loggingContext = loggingContext;

    public void ReportResult(BuildAnalyzerResult result)
    {
        BuildEventArgs eventArgs = result.ToEventArgs(ConfigurationProvider.GetMergedConfiguration(result.BuildAnalyzerRule).Severity);
        eventArgs.BuildEventContext = _loggingContext.BuildEventContext;
        _loggingContext.LogBuildEvent(eventArgs);
    }
}
