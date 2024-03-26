// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using Microsoft.Build.Experimental.BuildCheck;

namespace Microsoft.Build.BuildCheck.Infrastructure;

internal sealed class BuildCheckRegistrationContext(BuildAnalyzerWrapper analyzerWrapper, BuildCheckCentralContext buildCheckCentralContext) : IBuildCheckRegistrationContext
{
    private int _evaluatedPropertiesActionCount;
    private int _parsedItemsActionCount;

    public void RegisterEvaluatedPropertiesAction(Action<BuildCheckDataContext<EvaluatedPropertiesAnalysisData>> evaluatedPropertiesAction)
    {
        if (Interlocked.Increment(ref _evaluatedPropertiesActionCount) > 1)
        {
            throw new BuildCheckConfigurationException(
                $"Analyzer '{analyzerWrapper.BuildAnalyzer.FriendlyName}' attempted to call '{nameof(RegisterEvaluatedPropertiesAction)}' multiple times.");
        }

        buildCheckCentralContext.RegisterEvaluatedPropertiesAction(analyzerWrapper, evaluatedPropertiesAction);
    }

    public void RegisterParsedItemsAction(Action<BuildCheckDataContext<ParsedItemsAnalysisData>> parsedItemsAction)
    {
        if (Interlocked.Increment(ref _parsedItemsActionCount) > 1)
        {
            throw new BuildCheckConfigurationException(
                $"Analyzer '{analyzerWrapper.BuildAnalyzer.FriendlyName}' attempted to call '{nameof(RegisterParsedItemsAction)}' multiple times.");
        }

        buildCheckCentralContext.RegisterParsedItemsAction(analyzerWrapper, parsedItemsAction);
    }
}
