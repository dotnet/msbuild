// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Experimental.BuildCop;

public interface IBuildCopRegistrationContext
{
    void RegisterEvaluatedPropertiesAction(Action<BuildCopDataContext<EvaluatedPropertiesAnalysisData>> evaluatedPropertiesAction);
    void RegisterParsedItemsAction(Action<BuildCopDataContext<ParsedItemsAnalysisData>> parsedItemsAction);
}
