// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.Construction;
using System.Collections.Immutable;

namespace Microsoft.Build.Evaluation
{
    internal partial class LazyItemEvaluator<P, I, M, D>
    {
        class UpdateOperation : LazyItemOperation
        {
            private readonly ImmutableList<ProjectMetadataElement> _metadata;

            public UpdateOperation(OperationBuilderWithMetadata builder, LazyItemEvaluator<P, I, M, D> lazyEvaluator)
                : base(builder, lazyEvaluator)
            {
                _metadata = builder.Metadata.ToImmutable();
            }

            protected override ICollection<I> SelectItems(ImmutableList<ItemData>.Builder listBuilder, ImmutableHashSet<string> globsToIgnore)
            {
                return SelectItemsMatchingItemSpec(listBuilder, _itemElement.UpdateLocation);
            }

            protected override void MutateItems(ICollection<I> items)
            {
                DecorateItemsWithMetadata(items, _metadata);
            }
        }
    }
}
