// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Build.Construction;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    private abstract class OperationBuilderWithMetadata(ProjectItemElement itemElement, bool conditionResult)
        : OperationBuilder(itemElement, conditionResult)
    {
        private ImmutableArray<ProjectMetadataElement>.Builder? _metadata;

        public ImmutableArray<ProjectMetadataElement> GetMetadata()
            => _metadata?.ToImmutable() ?? [];

        public void AddMetadata(ProjectMetadataElement metadataElement)
        {
            _metadata ??= ImmutableArray.CreateBuilder<ProjectMetadataElement>();
            _metadata.Add(metadataElement);
        }
    }
}
