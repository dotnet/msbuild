// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

/// <summary>
/// Consumer of the data from the build engine.
/// Currently, this is used to send data for checks to the BuildCheck.
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
    // void ProcessCheckAcquisition(CheckAcquisitionData acquisitionData);
}
