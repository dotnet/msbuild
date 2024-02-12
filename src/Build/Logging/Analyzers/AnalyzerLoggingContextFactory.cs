// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Experimental;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.Analyzers;
internal class AnalyzerLoggingContextFactory(ILoggingService loggingService) : IBuildAnalysisLoggingContextFactory
{
    public IBuildAnalysisLoggingContext CreateLoggingContext(BuildEventContext eventContext) =>
        new AnalyzerLoggingContext(loggingService, eventContext);
}
