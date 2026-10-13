// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Build.Construction;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    private sealed class RemoveOperationBuilder(ProjectItemElement itemElement, bool conditionResult)
        : OperationBuilder(itemElement, conditionResult)
    {
        private ImmutableList<string>.Builder? _matchOnMetadata;

        public MatchOnMetadataOptions MatchOnMetadataOptions { get; set; }

        public ImmutableList<string> GetMatchOnMetadata()
            => _matchOnMetadata?.ToImmutable() ?? [];

        public void AddMatchOnMetadata(string value)
        {
            _matchOnMetadata ??= ImmutableList.CreateBuilder<string>();
            _matchOnMetadata.Add(value);
        }
    }
}
