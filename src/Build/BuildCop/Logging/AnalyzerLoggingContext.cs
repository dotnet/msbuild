// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Experimental.BuildCop;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BuildCop.Logging;

internal class AnalyzerLoggingContext : LoggingContext, IBuildAnalysisLoggingContext
{
    public AnalyzerLoggingContext(ILoggingService loggingService, BuildEventContext eventContext)
        : base(loggingService, eventContext)
    {
        IsValid = true;
    }

    public AnalyzerLoggingContext(LoggingContext baseContext) : base(baseContext)
    {
        IsValid = true;
    }
}
