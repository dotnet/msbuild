// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    public struct ItemData
    {
        public ItemData(I item, ProjectItemElement originatingItemElement, int elementOrder, bool conditionResult, string normalizedItemValue = null)
        {
            Item = item;
            OriginatingItemElement = originatingItemElement;
            ElementOrder = elementOrder;
            ConditionResult = conditionResult;
            _normalizedItemValue = normalizedItemValue;
        }

        public readonly ItemData Clone(IItemFactory<I, I> itemFactory, ProjectItemElement initialItemElementForFactory)
        {
            // setting the factory's item element to the original item element that produced the item
            // otherwise you get weird things like items that appear to have been produced by update elements
            itemFactory.ItemElement = OriginatingItemElement;
            var clonedItem = itemFactory.CreateItem(Item, OriginatingItemElement.ContainingProject.FullPath);
            itemFactory.ItemElement = initialItemElementForFactory;

            return new ItemData(clonedItem, OriginatingItemElement, ElementOrder, ConditionResult, _normalizedItemValue);
        }

        public I Item { get; }
        public ProjectItemElement OriginatingItemElement { get; }
        public int ElementOrder { get; }
        public bool ConditionResult { get; }

        /// <summary>
        /// Lazily created normalized item value.
        /// </summary>
        private string _normalizedItemValue;
        public string NormalizedItemValue
        {
            get
            {
                var normalizedItemValue = Volatile.Read(ref _normalizedItemValue);
                if (normalizedItemValue == null)
                {
                    normalizedItemValue = FileUtilities.NormalizePathForComparisonNoThrow(Item.EvaluatedInclude, Item.ProjectDirectory);
                    Volatile.Write(ref _normalizedItemValue, normalizedItemValue);
                }
                return normalizedItemValue;
            }
        }
    }
}
