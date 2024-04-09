// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BuildCheck.Logging;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Experimental.BuildCheck;

internal interface IBuildAnalysisLoggingContextFactory
{
    AnalyzerLoggingContext CreateLoggingContext(BuildEventContext eventContext);
}
