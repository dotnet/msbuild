// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

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
        private Dictionary<NormalizedItemPath, UpdateOperation>? _updates;

        /// <summary>
        ///  Tries to add a literal Update to the pending batch.
        /// </summary>
        /// <param name="operation">The Update to add.</param>
        /// <returns>
        ///  <see langword="true"/> if the Update was added; otherwise, <see langword="false"/>, leaving the pending batch unchanged.
        /// </returns>
        public bool TryAdd(UpdateOperation operation)
        {
            if (!operation.TryGetLiteralKeys(out var keys))
            {
                return false;
            }

            // Check every key first so rejection leaves the pending batch unchanged.
            if (_updates is not null)
            {
                foreach (NormalizedItemPath key in keys)
                {
                    if (_updates.ContainsKey(key))
                    {
                        return false;
                    }
                }
            }

            if (keys.IsEmpty)
            {
                return true;
            }

            _updates ??= [];
            foreach (NormalizedItemPath key in keys)
            {
                _updates.Add(key, operation);
            }

            return true;
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

            // The replacement runs immediately after a successful lookup, while its selected operation is current.
            UpdateOperation? operation = null;
            items.UpdateAll(
                itemData => updates.TryGetValue(itemData.NormalizedItemValue, out operation),
                itemData => operation!.UpdateItem(itemData));

            updates.Clear();
        }
    }
}
