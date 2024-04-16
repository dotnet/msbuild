// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BuildCheck.Logging;

internal class AnalyzerLoggingContext : LoggingContext
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
