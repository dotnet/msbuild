// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Experimental.BuildCop;

namespace Microsoft.Build.BuildCop.Infrastructure;

internal sealed class BuildCopContext(BuildAnalyzerTracingWrapper analyzer, BuildCopCentralContext buildCopCentralContext) : IBuildCopContext
{
    public void RegisterEvaluatedPropertiesAction(EvaluatedPropertiesAction evaluatedPropertiesAction)
    {
        void WrappedEvaluatedPropertiesAction(EvaluatedPropertiesContext context)
        {
            using var _ = analyzer.StartSpan();
            evaluatedPropertiesAction(context);
        }

        buildCopCentralContext.RegisterEvaluatedPropertiesAction(WrappedEvaluatedPropertiesAction);
    }

    public void RegisterParsedItemsAction(ParsedItemsAction parsedItemsAction)
    {
        void WrappedParsedItemsAction(ParsedItemsContext context)
        {
            using var _ = analyzer.StartSpan();
            parsedItemsAction(context);
        }

        buildCopCentralContext.RegisterParsedItemsAction(WrappedParsedItemsAction);
    }
}
