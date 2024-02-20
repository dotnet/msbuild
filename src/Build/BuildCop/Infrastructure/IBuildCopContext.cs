// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Experimental.BuildCop;

namespace Microsoft.Build.BuildCop.Infrastructure;

public interface IBuildCopContext
{
    void RegisterEvaluatedPropertiesAction(EvaluatedPropertiesAction evaluatedPropertiesAction);
    void RegisterParsedItemsAction(ParsedItemsAction parsedItemsAction);
}
