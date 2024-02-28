// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using Microsoft.Build.Experimental.BuildCop;

namespace Microsoft.Build.BuildCop.Infrastructure;

internal sealed class BuildCopContext(BuildAnalyzerWrapper analyzerWrapper, BuildCopCentralContext buildCopCentralContext) : IBuildCopContext
{
    private int _evaluatedPropertiesActionCount;
    private int _parsedItemsActionCount;

    public void RegisterEvaluatedPropertiesAction(Action<BuildAnalysisContext<EvaluatedPropertiesAnalysisData>> evaluatedPropertiesAction)
    {
        if (Interlocked.Increment(ref _evaluatedPropertiesActionCount) > 1)
        {
            throw new BuildCopConfigurationException(
                $"Analyzer '{analyzerWrapper.BuildAnalyzer.FriendlyName}' attempted to call '{nameof(RegisterEvaluatedPropertiesAction)}' multiple times.");
        }

        buildCopCentralContext.RegisterEvaluatedPropertiesAction(analyzerWrapper, evaluatedPropertiesAction);
    }

    public void RegisterParsedItemsAction(Action<BuildAnalysisContext<ParsedItemsAnalysisData>> parsedItemsAction)
    {
        if (Interlocked.Increment(ref _parsedItemsActionCount) > 1)
        {
            throw new BuildCopConfigurationException(
                $"Analyzer '{analyzerWrapper.BuildAnalyzer.FriendlyName}' attempted to call '{nameof(RegisterParsedItemsAction)}' multiple times.");
        }

        buildCopCentralContext.RegisterParsedItemsAction(analyzerWrapper, parsedItemsAction);
    }
}
