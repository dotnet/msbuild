// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    private sealed class UpdateOperationBuilder(ProjectItemElement itemElement, bool conditionResult)
        : OperationBuilderWithMetadata(itemElement, conditionResult)
    {
    }
}
