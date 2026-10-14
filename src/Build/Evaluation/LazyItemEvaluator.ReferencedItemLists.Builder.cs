// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Collections;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    private abstract partial class ReferencedItemLists
    {
        /// <summary>
        ///  Collects captured item lists in pooled storage and produces independently owned read-only mappings.
        /// </summary>
        public ref struct Builder
        {
            private RefArrayBuilder<Entry> _references;

            /// <summary>
            ///  Adds a captured list. Later additions for the same item type replace earlier ones in the completed mapping.
            /// </summary>
            /// <param name="itemType">The referenced item type.</param>
            /// <param name="itemList">The item list visible when the reference was captured.</param>
            public void Add(string itemType, ItemListReference itemList)
                => _references.Add(new(itemType, itemList));

            /// <summary>
            ///  Creates an independently owned read-only mapping of the captured item lists.
            /// </summary>
            /// <returns>
            ///  The captured item lists, or the shared empty mapping when no lists were added.
            /// </returns>
            public readonly ReferencedItemLists ToImmutable()
                => Create(_references.AsSpan());

            /// <summary>
            ///  Returns a reference to this builder so a scoped <see langword="using"/> local can be passed by reference.
            /// </summary>
            /// <returns>
            ///  A reference to this builder.
            /// </returns>
            [UnscopedRef]
            public ref Builder AsRef() => ref this;

            /// <summary>
            ///  Returns temporary storage to the pool without affecting completed mappings.
            /// </summary>
            public void Dispose() => _references.Dispose();
        }
    }
}
