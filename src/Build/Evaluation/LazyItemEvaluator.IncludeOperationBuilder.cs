// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;
using Microsoft.CodeAnalysis.Collections;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    private class IncludeOperationBuilder : OperationBuilderWithMetadata
    {
        public int ElementOrder { get; set; }
        public string? RootDirectory { get; set; }

        public ImmutableSegmentedList<string>.Builder Excludes { get; } = ImmutableSegmentedList.CreateBuilder<string>();

        public IncludeOperationBuilder(ProjectItemElement itemElement, bool conditionResult) : base(itemElement, conditionResult)
        {
        }
    }
}
