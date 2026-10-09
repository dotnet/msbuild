// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Build.Construction;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    private class OperationBuilderWithMetadata : OperationBuilder
    {
        public readonly ImmutableArray<ProjectMetadataElement>.Builder Metadata = ImmutableArray.CreateBuilder<ProjectMetadataElement>();

        public OperationBuilderWithMetadata(ProjectItemElement itemElement, bool conditionResult) : base(itemElement, conditionResult)
        {
        }
    }
}
