// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

/// <summary>
/// The module that routes the data to the final consumer.
/// Typically, it is the BuildCheck (in case of in-node analysis) or LoggingService
///  (in case of centralized analysis, where the data will first be wrapped to BuildEventArgs and sent to central node).
/// </summary>
internal interface IBuildEngineDataRouter
{
    void ProcessPropertyRead(
        PropertyReadInfo propertyReadInfo,
        // This is intentionally AnalysisLoggingContext instead of IAnalysisContext - to avoid boxing allocations
        //  on a hot path of properties reading (same for writing)
        AnalysisLoggingContext analysisContext);

    /// <summary>
    /// Signals that a property was written to.
    /// </summary>
    void ProcessPropertyWrite(
        PropertyWriteInfo propertyWriteInfo,
        AnalysisLoggingContext analysisContext);
}
