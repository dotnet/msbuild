// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
