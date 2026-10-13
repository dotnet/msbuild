// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;
using Microsoft.CodeAnalysis.Collections;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    private class IncludeOperationBuilder(ProjectItemElement itemElement, bool conditionResult)
        : OperationBuilderWithMetadata(itemElement, conditionResult)
    {
        private ImmutableSegmentedList<string>.Builder? _excludes;

        public int ElementOrder { get; init; }
        public string? RootDirectory { get; init; }

        public ImmutableSegmentedList<string> GetExcludes()
            => _excludes?.ToImmutable() ?? [];

        public void AddExclude(string exclude)
        {
            _excludes ??= ImmutableSegmentedList.CreateBuilder<string>();
            _excludes.Add(exclude);
        }
    }
}
