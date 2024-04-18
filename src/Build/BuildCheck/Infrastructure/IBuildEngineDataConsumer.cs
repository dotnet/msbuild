// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BuildCheck.Infrastructure;

/// <summary>
/// Consumer of the data from the build engine.
/// Currently, this is used to send data for analysis to the BuildCheck.
/// In the future we can multiplex the data to other consumers (e.g. copilot).
/// </summary>
internal interface IBuildEngineDataConsumer
{
    void ProcessPropertyRead(
        string propertyName,
        int startIndex,
        int endIndex,
        IMsBuildElementLocation elementLocation,
        bool isUninitialized,
        PropertyReadContext propertyReadContext,
        BuildEventContext? buildEventContext);

    /// <summary>
    /// Signals that a property was written to.
    /// </summary>
    /// <param name="propertyName">Name of the property.</param>
    /// <param name="isEmpty">Was any value written? (E.g. if we set propA with value propB, while propB is undefined - the isEmpty will be true)</param>
    /// <param name="elementLocation">Location of the property write</param>
    /// <param name="buildEventContext"></param>
    void ProcessPropertyWrite(
        string propertyName,
        bool isEmpty,
        IMsBuildElementLocation? elementLocation,
        BuildEventContext? buildEventContext);

    // TODO: We might want to move acquisition data processing into this interface as well
    // void ProcessAnalyzerAcquisition(AnalyzerAcquisitionData acquisitionData);
}
