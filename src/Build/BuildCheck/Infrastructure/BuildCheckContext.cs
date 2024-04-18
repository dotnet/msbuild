// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using Microsoft.Build.BuildCheck.Analyzers;
using Microsoft.Build.Experimental.BuildCheck;

namespace Microsoft.Build.BuildCheck.Infrastructure;

internal sealed class BuildCheckRegistrationContext(BuildAnalyzerWrapper analyzerWrapper, BuildCheckCentralContext buildCheckCentralContext) : IInternalBuildCheckRegistrationContext
{
    public void RegisterEvaluatedPropertiesAction(Action<BuildCheckDataContext<EvaluatedPropertiesAnalysisData>> evaluatedPropertiesAction)
    {
        buildCheckCentralContext.RegisterEvaluatedPropertiesAction(analyzerWrapper, evaluatedPropertiesAction);
    }

    public void RegisterParsedItemsAction(Action<BuildCheckDataContext<ParsedItemsAnalysisData>> parsedItemsAction)
    {
        buildCheckCentralContext.RegisterParsedItemsAction(analyzerWrapper, parsedItemsAction);
    }

    public void RegisterPropertyReadAction(Action<BuildCheckDataContext<PropertyReadData>> propertyReadAction)
        => buildCheckCentralContext.RegisterPropertyReadAction(analyzerWrapper, propertyReadAction);

    public void RegisterPropertyWriteAction(Action<BuildCheckDataContext<PropertyWriteData>> propertyWriteAction)
        => buildCheckCentralContext.RegisterPropertyWriteAction(analyzerWrapper, propertyWriteAction);

    public void RegisterProjectProcessingDoneAction(Action<BuildCheckDataContext<ProjectProcessingDoneData>> projectDoneAction)
        => buildCheckCentralContext.RegisterProjectProcessingDoneAction(analyzerWrapper, projectDoneAction);
}
