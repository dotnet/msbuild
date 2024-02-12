// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.Build.Experimental;

public interface IBuildAnalysisLoggingContextFactory
{
    IBuildAnalysisLoggingContext CreateLoggingContext(BuildEventContext eventContext);
}
