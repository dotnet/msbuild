// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Collections;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    private abstract partial class ItemListBookmarks
    {
        /// <summary>
        ///  Collects bookmarks in pooled storage and produces independently owned read-only mappings.
        /// </summary>
        public ref struct Builder
        {
            private RefArrayBuilder<Entry> _references;

            /// <summary>
            ///  Adds a bookmark. A later addition for the same item type replaces the earlier mapping.
            /// </summary>
            /// <param name="itemType">The referenced item type.</param>
            /// <param name="itemList">The position visible when the bookmark was created.</param>
            public void Add(string itemType, ItemListBookmark itemList)
                => _references.Add(new(itemType, itemList));

            /// <summary>
            ///  Creates an independently owned read-only mapping of item types to bookmarks.
            /// </summary>
            /// <returns>
            ///  The bookmarks, or the shared empty mapping when no bookmarks were added.
            /// </returns>
            public readonly ItemListBookmarks ToImmutable()
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
