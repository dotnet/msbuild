// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    /// <summary>
    ///  Collects captured item lists, allocating mutable storage only when a list is added.
    /// </summary>
    private struct ReferencedItemListBuilder
    {
        // WORKAROUND: Unnecessary boxed allocation: https://github.com/dotnet/corefx/issues/24563
        private static readonly ImmutableDictionary<string, LazyItemList> s_emptyIgnoreCase =
            ImmutableDictionary.Create<string, LazyItemList>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        ///  Gets the shared empty dictionary with the configured item-name comparer.
        /// </summary>
        public static ImmutableDictionary<string, LazyItemList> Empty => Traits.Instance.EscapeHatches.UseCaseSensitiveItemNames
            ? ImmutableDictionary<string, LazyItemList>.Empty
            : s_emptyIgnoreCase;

        private ImmutableDictionary<string, LazyItemList>.Builder? _builder;

        /// <summary>
        ///  Adds or replaces the captured list for an item type.
        /// </summary>
        /// <param name="itemType">The referenced item type.</param>
        /// <param name="itemList">The item list visible when the reference was captured.</param>
        public void Add(string itemType, LazyItemList itemList)
        {
            _builder ??= Empty.ToBuilder();
            _builder[itemType] = itemList;
        }

        /// <summary>
        ///  Returns an immutable snapshot without allocating mutable storage when no lists were added.
        /// </summary>
        /// <returns>
        ///  The captured item lists, or the shared empty dictionary with the selected comparer.
        /// </returns>
        public ImmutableDictionary<string, LazyItemList> ToImmutable()
            => _builder?.ToImmutable() ?? Empty;
    }
}
