// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Build.Construction;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    private abstract class OperationBuilder(ProjectItemElement itemElement, bool conditionResult)
    {
        private ReferencedItemListBuilder? _referencedItemListBuilder;

        public ProjectItemElement ItemElement { get; } = itemElement;
        public bool ConditionResult { get; } = conditionResult;

        public ItemSpec<P, I>? ItemSpec { get; set; }

        /// <summary>
        ///  Gets an immutable snapshot without creating a builder when no item lists were captured.
        /// </summary>
        public ImmutableDictionary<string, LazyItemList> GetReferencedItemLists()
            => _referencedItemListBuilder?.ToImmutable() ?? ReferencedItemListBuilder.Empty;

        /// <summary>
        ///  Adds or replaces a captured item list, creating the builder on the first addition.
        /// </summary>
        /// <param name="itemType">The referenced item type.</param>
        /// <param name="itemList">The item list visible when the reference was captured.</param>
        public void AddReferencedItemList(string itemType, LazyItemList itemList)
            => (_referencedItemListBuilder ??= new()).Add(itemType, itemList);
    }
}
