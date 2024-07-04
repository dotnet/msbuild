// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

internal readonly record struct PropertyReadInfo(
    string PropertyName,
    int StartIndex,
    int EndIndex,
    IMsBuildElementLocation ElementLocation,
    bool IsUninitialized,
    PropertyReadContext PropertyReadContext);

/// <summary>
/// 
/// </summary>
/// <param name="PropertyName">Name of the property.</param>
/// <param name="IsEmpty">Was any value written? (E.g. if we set propA with value propB, while propB is undefined - the isEmpty will be true)</param>
/// <param name="ElementLocation">Location of the property write</param>
internal readonly record struct PropertyWriteInfo(
    string PropertyName,
    bool IsEmpty,
    IMsBuildElementLocation? ElementLocation);

/// <summary>
/// Consumer of the data from the build engine.
/// Currently, this is used to send data for analysis to the BuildCheck.
/// In the future we can multiplex the data to other consumers (e.g. copilot).
/// </summary>
internal interface IBuildEngineDataConsumer
{
    void ProcessPropertyRead(PropertyReadInfo propertyReadInfo);
    
    /// <summary>
    /// Signals that a property was written to.
    /// </summary>
    /// <param name="propertyWriteInfo">Name of the property.</param>
    void ProcessPropertyWrite(PropertyWriteInfo propertyWriteInfo);

    // TODO: We might want to move acquisition data processing into this interface as well
    // void ProcessAnalyzerAcquisition(AnalyzerAcquisitionData acquisitionData);
}

/// <summary>
/// The module that routes the data to the final consumer.
/// Typically, it is the BuildCheck (in case of in-node analysis) or LoggingService
///  (in case of centralized analysis, where the data will first be wrapped to BuildEventArgs and sent to central node).
/// </summary>
internal interface IBuildEngineDataRouter
{
    void ProcessPropertyRead(
        PropertyReadInfo propertyReadInfo,
        AnalysisLoggingContext analysisContext);

    /// <summary>
    /// Signals that a property was written to.
    /// </summary>
    void ProcessPropertyWrite(
        PropertyWriteInfo propertyWriteInfo,
        AnalysisLoggingContext analysisContext);
}
