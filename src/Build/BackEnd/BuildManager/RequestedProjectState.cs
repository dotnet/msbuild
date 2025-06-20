// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.BackEnd;

#nullable disable

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// Interface defining properties, items, and metadata of interest for a <see cref="BuildRequestData"/>.
    /// </summary>
    public class RequestedProjectState : ITranslatable
    {
        private List<string> _propertyFilters;
        private IDictionary<string, List<string>> _itemFilters;

        /// <summary>
        /// Properties of interest.
        /// </summary>
        public List<string> PropertyFilters
        {
            get => _propertyFilters;
            set => _propertyFilters = value;
        }

        /// <summary>
        /// Items and metadata of interest.
        /// </summary>
        public IDictionary<string, List<string>> ItemFilters
        {
            get => _itemFilters;
            set => _itemFilters = value;
        }

        /// <summary>
        /// Creates a deep copy of this instance.
        /// </summary>
        internal RequestedProjectState DeepClone()
        {
            RequestedProjectState result = new RequestedProjectState();
            if (PropertyFilters is not null)
            {
                result.PropertyFilters = [.. PropertyFilters];
            }

            if (ItemFilters is not null)
            {
                result.ItemFilters = ItemFilters.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value == null ? null : new List<string>(kvp.Value));
            }

            return result;
        }

        /// <summary>
        /// Returns a new RequestedProjectState that contains the union of filters from this instance and another.
        /// </summary>
        /// <param name="other">The other RequestedProjectState to merge with.</param>
        /// <returns>A new RequestedProjectState representing the merged filters.</returns>
        internal RequestedProjectState Merge(RequestedProjectState other)
        {
            // If either is null, return a clone of the non-null one
            if (other == null)
            {
                return DeepClone();
            }

            RequestedProjectState result = new RequestedProjectState();

            // Merge property filters
            if (PropertyFilters != null || other.PropertyFilters != null)
            {
                HashSet<string> mergedProperties;

                if (PropertyFilters != null)
                {
                    mergedProperties = new HashSet<string>(PropertyFilters, StringComparer.OrdinalIgnoreCase);
                    if (other.PropertyFilters != null)
                    {
                        mergedProperties.UnionWith(other.PropertyFilters);
                    }
                }
                else
                {
                    mergedProperties = new HashSet<string>(other.PropertyFilters, StringComparer.OrdinalIgnoreCase);
                }

                if (mergedProperties.Count > 0)
                {
                    result.PropertyFilters = mergedProperties.ToList();
                }
            }

            // Merge item filters
            if (ItemFilters != null || other.ItemFilters != null)
            {
                Dictionary<string, List<string>> mergedItems = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

                // Add items from both filters
                MergeItemFiltersFrom(mergedItems, ItemFilters);
                MergeItemFiltersFrom(mergedItems, other.ItemFilters);

                if (mergedItems.Count > 0)
                {
                    result.ItemFilters = mergedItems;
                }
            }

            return result;

            // Helper method to add item filters from a source to the merged result
            void MergeItemFiltersFrom(Dictionary<string, List<string>> mergedItems, IDictionary<string, List<string>> source)
            {
                if (source == null)
                {
                    return;
                }

                foreach (var itemType in source)
                {
                    if (!mergedItems.TryGetValue(itemType.Key, out List<string> metadataList))
                    {
                        if (itemType.Value != null)
                        {
                            metadataList = new List<string>(itemType.Value);
                            mergedItems[itemType.Key] = metadataList;
                        }
                        else
                        {
                            metadataList = new List<string>();
                            mergedItems[itemType.Key] = metadataList;
                        }
                    }
                    else if (itemType.Value != null)
                    {
                        HashSet<string> existingMetadata = new HashSet<string>(metadataList, StringComparer.OrdinalIgnoreCase);
                        foreach (var metadata in itemType.Value)
                        {
                            if (existingMetadata.Add(metadata))
                            {
                                metadataList.Add(metadata);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns true if this instance contains all property and item filters present in another instance.
        /// </summary>
        /// <param name="another">The instance to compare against.</param>
        /// <returns>True if this instance is equivalent or a strict subset of <paramref name="another"/>.</returns>
        internal bool IsSubsetOf(RequestedProjectState another)
        {
            if (PropertyFilters is null)
            {
                if (another.PropertyFilters is not null)
                {
                    // The instance to compare against has filtered props and we need everything -> not a subset.
                    return false;
                }
            }
            else if (another.PropertyFilters is not null)
            {
                HashSet<string> thisPropertyFilters = new HashSet<string>(PropertyFilters, StringComparer.OrdinalIgnoreCase);
                HashSet<string> anotherPropertyFilters = new HashSet<string>(another.PropertyFilters, StringComparer.OrdinalIgnoreCase);

                if (!thisPropertyFilters.IsSubsetOf(anotherPropertyFilters))
                {
                    return false;
                }
            }

            if (ItemFilters is null)
            {
                if (another.ItemFilters is not null)
                {
                    // The instance to compare against has filtered items and we need everything -> not a subset.
                    return false;
                }
            }
            else if (another.ItemFilters is not null)
            {
                foreach (KeyValuePair<string, List<string>> kvp in ItemFilters)
                {
                    if (!another.ItemFilters.TryGetValue(kvp.Key, out List<string> metadata))
                    {
                        // The instance to compare against doesn't have this item -> not a subset.
                        return false;
                    }

                    if (kvp.Value is null)
                    {
                        if (metadata is not null)
                        {
                            // The instance to compare against has filtered metadata for this item and we need everything - not a subset.
                            return false;
                        }
                    }
                    else if (metadata is not null)
                    {
                        HashSet<string> thisMetadata = new HashSet<string>(kvp.Value, StringComparer.OrdinalIgnoreCase);
                        HashSet<string> anotherMetadata = new HashSet<string>(metadata, StringComparer.OrdinalIgnoreCase);
                        
                        if (!thisMetadata.IsSubsetOf(anotherMetadata))
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        void ITranslatable.Translate(ITranslator translator)
        {
            translator.Translate(ref _propertyFilters);
            translator.TranslateDictionary(ref _itemFilters, TranslateString, TranslateMetadataForItem, CreateItemMetadataDictionary);
        }

        private static IDictionary<string, List<string>> CreateItemMetadataDictionary(int capacity) => new Dictionary<string, List<string>>(capacity, StringComparer.OrdinalIgnoreCase);

        private static void TranslateMetadataForItem(ITranslator translator, ref List<string> list) => translator.Translate(ref list);

        private static void TranslateString(ITranslator translator, ref string s) => translator.Translate(ref s);
    }
}
