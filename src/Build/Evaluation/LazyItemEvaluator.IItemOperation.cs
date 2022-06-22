// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    internal partial class LazyItemEvaluator<P, I, M, D>
    {
        internal interface IItemOperation
        {
            void Apply(OrderedItemDataCollection.Builder listBuilder, ImmutableHashSet<string> globsToIgnore);
        }
    }
}
