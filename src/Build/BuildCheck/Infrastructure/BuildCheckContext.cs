// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

internal sealed class BuildCheckRegistrationContext(BuildAnalyzerWrapper analyzerWrapper, BuildCheckCentralContext buildCheckCentralContext) : IBuildCheckRegistrationContext
{
    public void RegisterEvaluatedPropertiesAction(Action<BuildCheckDataContext<EvaluatedPropertiesAnalysisData>> evaluatedPropertiesAction)
    {
        buildCheckCentralContext.RegisterEvaluatedPropertiesAction(analyzerWrapper, evaluatedPropertiesAction);
    }

    public void RegisterParsedItemsAction(Action<BuildCheckDataContext<ParsedItemsAnalysisData>> parsedItemsAction)
    {
        buildCheckCentralContext.RegisterParsedItemsAction(analyzerWrapper, parsedItemsAction);
    }

    public void RegisterTaskInvocationAction(Action<BuildCheckDataContext<TaskInvocationAnalysisData>> taskInvocationAction)
    {
        buildCheckCentralContext.RegisterTaskInvocationAction(analyzerWrapper, taskInvocationAction);
    }
}
