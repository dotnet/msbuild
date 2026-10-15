// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    /// <summary>
    ///  Collects disjoint literal Updates and applies them with one scan of the current items.
    /// </summary>
    /// <remarks>
    ///  Default initialization does not allocate. The dictionary is created on the first literal-path insertion
    ///  and reused after applying each batch. Do not copy an active batch; copies share its mutable dictionary.
    /// </remarks>
    private struct UpdateBatch
    {
        private Dictionary<string, UpdateOperation>? _updates;

        /// <summary>
        ///  Tries to add a literal Update to the pending batch.
        /// </summary>
        /// <param name="operation">The Update to add.</param>
        /// <returns>
        ///  <see langword="true"/> if the Update was added; otherwise, <see langword="false"/>, leaving the pending batch unchanged.
        /// </returns>
        public bool TryAdd(UpdateOperation operation)
        {
            int fragmentIndex = 0;
            for (; fragmentIndex < operation.Spec.Fragments.Count; fragmentIndex++)
            {
                ItemSpecFragment fragment = operation.Spec.Fragments[fragmentIndex];
                if (MSBuildConstants.CharactersForExpansion.Any(fragment.TextFragment.Contains))
                {
                    // Fragment contains wild cards, items, or properties. Cannot batch over it using a dictionary.
                    break;
                }

                string fullPath = FileUtilities.NormalizePathForComparisonNoThrow(fragment.TextFragment, fragment.ProjectDirectory);
                _updates ??= [with(StringComparer.OrdinalIgnoreCase)];
                if (_updates.ContainsKey(fullPath))
                {
                    // Another update will already happen on this path. Make that happen before evaluating this one.
                    break;
                }

                _updates.Add(fullPath, operation);
            }

            if (fragmentIndex == operation.Spec.Fragments.Count)
            {
                return true;
            }

            // Remove only this Update's tentative entries, using the same normalized keys as insertion.
            for (int i = 0; i < fragmentIndex; i++)
            {
                ItemSpecFragment fragment = operation.Spec.Fragments[i];
                string fullPath = FileUtilities.NormalizePathForComparisonNoThrow(fragment.TextFragment, fragment.ProjectDirectory);
                _updates!.Remove(fullPath);
            }

            return false;
        }

        /// <summary>
        ///  Applies pending Updates and clears the batch while retaining its dictionary for reuse.
        /// </summary>
        /// <param name="items">The current items to update.</param>
        public void ApplyTo(OrderedItemDataCollection.Builder items)
        {
            if (_updates is not { Count: > 0 } updates)
            {
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                string fullPath = FileUtilities.NormalizePathForComparisonNoThrow(items[i].Item.EvaluatedInclude, items[i].Item.ProjectDirectory);
                if (updates.TryGetValue(fullPath, out UpdateOperation? operation))
                {
                    items[i] = operation.UpdateItem(items[i]);
                }
            }

            updates.Clear();
        }
    }
}
