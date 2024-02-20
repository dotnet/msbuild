// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Experimental.BuildCop;

namespace Microsoft.Build.BuildCop.Logging;

internal static class BuildAnalysisLoggingContextExtensions
{
    public static LoggingContext ToLoggingContext(this IBuildAnalysisLoggingContext loggingContext) =>
        loggingContext as AnalyzerLoggingContext ??
        throw new InvalidOperationException("The logging context is not an AnalyzerLoggingContext");
}
