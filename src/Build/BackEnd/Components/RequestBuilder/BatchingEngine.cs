// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Internal;
using ElementLocation = Microsoft.Build.Construction.ElementLocation;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// This class is used by objects in the build engine that have the ability to execute themselves in batches, to partition the
    /// items they consume into "buckets", based on the values of select item metadata.
    /// </summary>
    /// <remarks>
    /// What batching does
    ///
    /// Batching partitions the items consumed by the batchable object into buckets, where each bucket
    /// contains a set of items that have the same value set on all item metadata consumed by the object.
    /// Metadata consumed may be unqualified, for example %(m), or qualified by the item list to which it
    /// refers, for example %(a.m).
    ///
    /// If metadata is qualified, for example %(a.m), then this is considered distinct to metadata with the
    /// same name on a different item type. For example, %(a.m) is distinct to %(b.m), and items of type ‘b’
    /// are considered to always have a blank value for %(a.m). This means items of type ‘b’ will only be
    /// placed in buckets where %(a.m) is blank. However %(a.m) is equivalent to %(m) on items of type ‘a’.
    ///
    /// There is an extra ambiguity rule: every items consumed by the object must have an explicit value for
    /// every piece of unqualified metadata. For example, if @(a), %(m), and %(a.n) are consumed, every item
    /// of type ‘a’ must have a value for the metadata ‘m’ but need not all necessarily have a value for the
    /// metadata ‘n’. This rule eliminates ambiguity about whether items that do not define values for an
    /// unqualified metadata should go in all buckets, or just into buckets with a blank value for
    /// that metadata.
    ///
    /// For example
    ///
    /// <ItemGroup>
    /// <a Include='a1;a2'>
    ///   <n>m0</n>
    /// </a>
    /// <a Include='a3'>
    ///   <n>m1</n>
    /// </a>
    /// <b Include='b1'>
    ///   <n>n0</n>
    /// </b>
    /// <b Include='b2;b3'>
    ///   <n>n1</n>
    /// </b>
    /// <b Include='b4'/>
    /// </ItemGroup>
    ///
    /// <Target Name="t" >
    ///   <Message Text="a={@(a).%(a.n)} b={@(b).%(b.n)}" />
    /// </Target>
    ///
    /// Will produce 5 buckets:
    ///
    /// a={a1;a2.m0} b={.}
    /// a={a3.m1} b={.}
    /// a={.} b={b1.n0}
    /// a={.} b={b2;b3.n1}
    /// a={.} b={b4.}
    ///
    /// </remarks>
    internal static class BatchingEngine
    {
        #region Methods

        /// <summary>
        /// Determines how many times the batchable object needs to be executed (each execution is termed a "batch"), and prepares
        /// buckets of items to pass to the object in each batch.
        /// </summary>
        /// <returns>List containing ItemBucket objects, each one representing an execution batch.</returns>
        internal static List<ItemBucket> PrepareBatchingBuckets(
            List<string> batchableObjectParameters,
            Lookup lookup,
            ElementLocation elementLocation,
            LoggingContext loggingContext)
        {
            return PrepareBatchingBuckets(batchableObjectParameters, lookup, null, elementLocation, loggingContext);
        }

        /// <summary>
        /// Determines how many times the batchable object needs to be executed (each execution is termed a "batch"), and prepares
        /// buckets of items to pass to the object in each batch.
        /// </summary>
        /// <param name="elementLocation"></param>
        /// <param name="batchableObjectParameters"></param>
        /// <param name="lookup"></param>
        /// <param name="implicitBatchableItemType">Any item type that can be considered an implicit input to this batchable object.
        /// This is useful for items inside targets, where the item name is plainly an item type that's an "input" to the object.</param>
        /// <param name="loggingContext"></param>
        /// <returns>List containing ItemBucket objects, each one representing an execution batch.</returns>
        internal static List<ItemBucket> PrepareBatchingBuckets(
            List<string> batchableObjectParameters,
            Lookup lookup,
            string implicitBatchableItemType,
            ElementLocation elementLocation,
            LoggingContext loggingContext)
            => PrepareBatchingBuckets(batchableObjectParameters, lookup, implicitBatchableItemType, elementLocation, loggingContext, returnSingleItemTypeBatches: false, out _, out _);

        /// <summary>
        /// Prepares buckets like <see cref="PrepareBatchingBuckets(List{string}, Lookup, string, ElementLocation, LoggingContext)"/>,
        /// unless the batchable object batches the items of a single item type into more than one batch. Then it returns those
        /// batches instead, without creating a lookup scope and an expander for each of them.
        /// </summary>
        /// <param name="batchableObjectParameters">The parameters of the batchable object.</param>
        /// <param name="lookup">The lookup that provides the items.</param>
        /// <param name="implicitBatchableItemType">Any item type that can be considered an implicit input to the batchable object.</param>
        /// <param name="elementLocation">The location of the batchable object.</param>
        /// <param name="loggingContext">The logging context for the expanders of the buckets.</param>
        /// <param name="batchedItemType">The item type of the items in <paramref name="batches"/>.</param>
        /// <param name="batches">The batches, in the order in which they are to be executed, or null if buckets are returned.</param>
        /// <returns>The buckets, or null if <paramref name="batches"/> are returned instead.</returns>
        internal static List<ItemBucket> PrepareBatchingBucketsOrBatches(
            List<string> batchableObjectParameters,
            Lookup lookup,
            string implicitBatchableItemType,
            ElementLocation elementLocation,
            LoggingContext loggingContext,
            out string batchedItemType,
            out List<ItemBatch> batches)
            => PrepareBatchingBuckets(batchableObjectParameters, lookup, implicitBatchableItemType, elementLocation, loggingContext, returnSingleItemTypeBatches: true, out batchedItemType, out batches);

        /// <summary>
        /// Creates a bucket for each batch of items of the specified item type.
        /// </summary>
        internal static List<ItemBucket> CreateBuckets(string batchedItemType, List<ItemBatch> batches, Lookup lookup, LoggingContext loggingContext)
        {
            FrozenSet<string> itemNames = new[] { batchedItemType }.ToFrozenSet(MSBuildNameIgnoreCaseComparer.Default);
            var buckets = new List<ItemBucket>(batches.Count);

            for (int i = 0; i < batches.Count; i++)
            {
                var bucket = new ItemBucket(itemNames, batches[i].Metadata, lookup, i);
                if (loggingContext != null)
                {
                    bucket.Initialize(loggingContext);
                }

                foreach (ProjectItemInstance item in batches[i].Items)
                {
                    bucket.AddItem(item);
                }

                buckets.Add(bucket);
            }

            return buckets;
        }

        private static List<ItemBucket> PrepareBatchingBuckets(
            List<string> batchableObjectParameters,
            Lookup lookup,
            string implicitBatchableItemType,
            ElementLocation elementLocation,
            LoggingContext loggingContext,
            bool returnSingleItemTypeBatches,
            out string batchedItemType,
            out List<ItemBatch> batches)
        {
            Assumed.NotNull(batchableObjectParameters, "Need the parameters of the batchable object to determine if it can be batched.");
            Assumed.NotNull(lookup, "Need to specify the lookup.");

            batchedItemType = null;
            batches = null;

            ItemsAndMetadataPair pair = ExpressionShredder.GetReferencedItemNamesAndMetadata(batchableObjectParameters);

            // All the @(itemname) item list references in the tag, including transforms, etc.
            HashSet<string> consumedItemReferences = pair.Items;

            // All the %(itemname.metadataname) references in the tag (not counting those embedded
            // inside item transforms), and note that the itemname portion is optional.
            // The keys in the returned hash table are the qualified metadata names (e.g. "EmbeddedResource.Culture"
            // or just "Culture").  The values are MetadataReference structs, which simply split out the item
            // name (possibly null) and the actual metadata name.
            Dictionary<string, MetadataReference> consumedMetadataReferences = pair.Metadata;

            List<ItemBucket> buckets = null;
            if (consumedMetadataReferences?.Count > 0)
            {
                // Add any item types that we were explicitly told to assume.
                if (implicitBatchableItemType != null)
                {
                    consumedItemReferences ??= new HashSet<string>(MSBuildNameIgnoreCaseComparer.Default);
                    consumedItemReferences.Add(implicitBatchableItemType);
                }

                // This method goes through all the item list references and figures out which ones
                // will be participating in batching, and which ones won't.  We get back a hashtable
                // where the key is the item name that will be participating in batching.  The values
                // are all String.Empty (not used).  This method may return additional item names
                // that weren't represented in "consumedItemReferences"... this would happen if there
                // were qualified metadata references in the consumedMetadataReferences table, such as
                // %(EmbeddedResource.Culture).
                Dictionary<string, ICollection<ProjectItemInstance>> itemListsToBeBatched = GetItemListsToBeBatched(consumedMetadataReferences, consumedItemReferences, lookup, elementLocation);

                // At this point, if there were any metadata references in the tag, but no item
                // references to batch on, we've got a problem because we can't figure out which
                // item lists the user wants us to batch.
                if (itemListsToBeBatched.Count == 0)
                {
                    foreach (string unqualifiedMetadataName in consumedMetadataReferences.Keys)
                    {
                        // Of course, since this throws an exception, there's no way we're ever going
                        // to really loop here... it's just that the foreach is the only way I can
                        // figure out how to get data out of the hashtable without knowing any of the
                        // keys!
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotReferenceItemMetadataWithoutItemName", unqualifiedMetadataName);
                    }
                }
                else if (returnSingleItemTypeBatches && itemListsToBeBatched.Count == 1)
                {
                    foreach (KeyValuePair<string, ICollection<ProjectItemInstance>> entry in itemListsToBeBatched)
                    {
                        // Use the previously-fetched items, if possible
                        List<ItemBatch> itemBatches = BatchItems(entry.Value ?? lookup.GetItems(entry.Key), consumedMetadataReferences, elementLocation);

                        if (itemBatches.Count > 1)
                        {
                            batchedItemType = entry.Key;
                            batches = itemBatches;
                            return null;
                        }

                        buckets = CreateBuckets(entry.Key, itemBatches, lookup, loggingContext);
                    }
                }
                else
                {
                    // If the batchable object consumes item metadata as well as items to be batched,
                    // we need to partition the items consumed by the object.
                    buckets = BucketConsumedItems(lookup, itemListsToBeBatched, consumedMetadataReferences, elementLocation, loggingContext);
                }
            }

            // if the batchable object does not consume any item metadata or items, or if the item lists it consumes are all
            // empty, then the object does not need to be batched
            if ((buckets == null) || (buckets.Count == 0))
            {
                // create a default bucket that references the project items and properties -- this way we always have a bucket
                buckets = new List<ItemBucket>(1);
                var bucket = new ItemBucket(null, null, lookup, buckets.Count);
                if (loggingContext != null)
                {
                    bucket.Initialize(loggingContext);
                }
                buckets.Add(bucket);
            }

            return buckets;
        }

        /// <summary>
        /// Of all the item lists that are referenced in this batchable object, which ones should we
        /// batch on, and which ones should we just pass in wholesale to every invocation of the
        /// target/task?
        ///
        /// Rule #1.  If the user has referenced any *qualified* item metadata such as %(EmbeddedResource.Culture),
        /// then that item list "EmbeddedResource" will definitely get batched.
        ///
        /// Rule #2.  For all the unqualified item metadata such as %(Culture), we make sure that
        /// every single item in every single item list being passed into the task contains a value
        /// for that metadata.  If not, it's an error.  If so, we batch all of those item lists.
        ///
        /// All other item lists will not be batched, and instead will be passed in wholesale to all buckets.
        /// </summary>
        /// <returns>Dictionary containing the item names that should be batched.  If the items match unqualified metadata,
        /// the entire list of items will be returned in the Value.  Otherwise, the Value will be empty, indicating only the
        /// qualified item set (in the Key) should be batched.
        /// </returns>
        private static Dictionary<string, ICollection<ProjectItemInstance>> GetItemListsToBeBatched(
            Dictionary<string, MetadataReference> consumedMetadataReferences,   // Key is [string] potentially qualified metadata name
                                                                                // Value is [struct MetadataReference]
            HashSet<string> consumedItemReferenceNames,
            Lookup lookup,
            ElementLocation elementLocation)
        {
            // The keys in this hashtable are the names of the items that we will batch on.
            // The values are always String.Empty (not used).
            var itemListsToBeBatched = new Dictionary<string, ICollection<ProjectItemInstance>>(MSBuildNameIgnoreCaseComparer.Default);

            // Loop through all the metadata references and find the ones that are qualified
            // with an item name.
            foreach (MetadataReference consumedMetadataReference in consumedMetadataReferences.Values)
            {
                if (consumedMetadataReference.ItemName != null)
                {
                    // Rule #1.  Qualified metadata reference.
                    // For metadata references that are qualified with an item name
                    // (e.g., %(EmbeddedResource.Culture) ), we add that item name to the list of
                    // consumed item names, even if the item name wasn't otherwise referenced via
                    // @(...) syntax, and even if every item in the list doesn't necessary contain
                    // a value for this metadata.  This is the special power that you get by qualifying
                    // the metadata reference with an item name.
                    itemListsToBeBatched[consumedMetadataReference.ItemName] = null;

                    // Also add this qualified item to the consumed item references list, because
                    // %(EmbeddedResource.Culture) effectively means that @(EmbeddedResource) is
                    // being consumed, even though we may not see literally "@(EmbeddedResource)"
                    // in the tag anywhere.  Adding it to this list allows us (down below in this
                    // method) to check that every item in this list has a value for each
                    // unqualified metadata reference.
                    consumedItemReferenceNames ??= new HashSet<string>(MSBuildNameIgnoreCaseComparer.Default);
                    consumedItemReferenceNames.Add(consumedMetadataReference.ItemName);
                }
            }

            // Loop through all the metadata references and find the ones that are unqualified.
            foreach (MetadataReference consumedMetadataReference in consumedMetadataReferences.Values)
            {
                if (consumedMetadataReference.ItemName == null)
                {
                    // Rule #2.  Unqualified metadata reference.
                    // For metadata references that are unqualified, every single consumed item
                    // must contain a value for that metadata.  If any item doesn't, it's an error
                    // to use unqualified metadata.
                    if (consumedItemReferenceNames != null)
                    {
                        foreach (string consumedItemName in consumedItemReferenceNames)
                        {
                            // Loop through all the items in the item list.
                            ICollection<ProjectItemInstance> items = lookup.GetItems(consumedItemName);

                            if (items != null)
                            {
                                // Loop through all the items in the BuildItemGroup.
                                foreach (ProjectItemInstance item in items)
                                {
                                    ProjectErrorUtilities.VerifyThrowInvalidProject(
                                        item.HasMetadata(consumedMetadataReference.MetadataName),
                                        elementLocation, "ItemDoesNotContainValueForUnqualifiedMetadata",
                                        item.EvaluatedInclude, consumedItemName, consumedMetadataReference.MetadataName);
                                }
                            }

                            // This item list passes the test of having every single item containing
                            // a value for this metadata.  Therefore, add this item list to the batching list.
                            // Also, to save doing lookup.GetItems again, put the items in the table as the value.
                            itemListsToBeBatched[consumedItemName] = items;
                        }
                    }
                }
            }

            return itemListsToBeBatched;
        }

        /// <summary>
        /// Partitions the items consumed by the batchable object into buckets, where each bucket contains a set of items that
        /// have the same value set on all item metadata consumed by the object.
        /// </summary>
        /// <remarks>
        /// PERF NOTE: Given n items and m batching metadata, the matching bucket for each item is found by hashing its m
        /// metadata values, so bucketing is O(n*m) even with single-item batching, where there is one bucket per item.
        /// Buckets are created in the order in which their first item is found, which is the order in which they are executed.
        /// </remarks>
        /// <returns>List containing ItemBucket objects (can be empty), each one representing an execution batch.</returns>
        private static List<ItemBucket> BucketConsumedItems(
            Lookup lookup,
            Dictionary<string, ICollection<ProjectItemInstance>> itemListsToBeBatched,
            Dictionary<string, MetadataReference> consumedMetadataReferences,
            ElementLocation elementLocation,
            LoggingContext loggingContext)
        {
            Assumed.Positive(itemListsToBeBatched.Count, "Need item types consumed by the batchable object.");
            Assumed.Positive(consumedMetadataReferences.Count, "Need item metadata consumed by the batchable object.");

            var buckets = new List<ItemBucket>();
            FrozenSet<string> itemNames = itemListsToBeBatched.Keys.ToFrozenSet(MSBuildNameIgnoreCaseComparer.Default);
            Dictionary<Dictionary<string, string>, int> bucketIndexes = new(MetadataValuesComparer.Instance);

            // Get and iterate through the list of item names that we're supposed to batch on.
            foreach (KeyValuePair<string, ICollection<ProjectItemInstance>> entry in itemListsToBeBatched)
            {
                string itemName = entry.Key;

                // Use the previously-fetched items, if possible
                ICollection<ProjectItemInstance> items = entry.Value ?? lookup.GetItems(itemName);

                if (items != null)
                {
                    buckets.EnsureCapacity(buckets.Count + items.Count);

                    foreach (ProjectItemInstance item in items)
                    {
                        // Get this item's values for all the metadata consumed by the batchable object.
                        Dictionary<string, string> itemMetadataValues = GetItemMetadataValues(item, consumedMetadataReferences, elementLocation);

                        ItemBucket matchingBucket;

                        // Find the bucket whose items have the same values as this item for all metadata consumed by the
                        // batchable object, or create one. New buckets are appended, so the first item declared in the
                        // project file ends up in the first batch passed into the target/task.
                        if (bucketIndexes.TryGetValue(itemMetadataValues, out int matchingBucketIndex))
                        {
                            matchingBucket = buckets[matchingBucketIndex];
                        }
                        else
                        {
                            matchingBucket = new ItemBucket(itemNames, itemMetadataValues, lookup, buckets.Count);
                            if (loggingContext != null)
                            {
                                matchingBucket.Initialize(loggingContext);
                            }

                            bucketIndexes.Add(itemMetadataValues, buckets.Count);
                            buckets.Add(matchingBucket);
                        }

                        // We already have a bucket for this type of item, so add this item to
                        // the bucket.
                        matchingBucket.AddItem(item);
                    }
                }
            }

            return buckets;
        }

        /// <summary>
        /// Gets the values of the specified metadata for the given item.
        /// The keys in the dictionary returned may be qualified and/or unqualified, exactly
        /// as they are found in the metadata reference.
        /// For example if %(x) is found, the key is "x", if %(z.x) is found, the key is "z.x".
        /// This dictionary in each bucket is used by Expander to expand exactly the same metadata references, so
        /// %(x) is expanded using the key "x", and %(z.x) is expanded using the key "z.x".
        /// </summary>
        /// <returns>the metadata values</returns>
        private static Dictionary<string, string> GetItemMetadataValues(
            ProjectItemInstance item,
            Dictionary<string, MetadataReference> consumedMetadataReferences,
            ElementLocation elementLocation)
        {
            var itemMetadataValues = new Dictionary<string, string>(consumedMetadataReferences.Count, MSBuildNameIgnoreCaseComparer.Default);

            foreach (KeyValuePair<string, MetadataReference> consumedMetadataReference in consumedMetadataReferences)
            {
                string metadataQualifiedName = consumedMetadataReference.Key;
                string metadataItemName = consumedMetadataReference.Value.ItemName;
                string metadataName = consumedMetadataReference.Value.MetadataName;

                if (
                        (metadataItemName != null) &&
                        (!String.Equals(item.ItemType, metadataItemName, StringComparison.OrdinalIgnoreCase)))
                {
                    itemMetadataValues[metadataQualifiedName] = String.Empty;
                }
                else
                {
                    try
                    {
                        // This returns String.Empty for both metadata that is undefined and metadata that has
                        // an empty value; they are treated the same.
                        itemMetadataValues[metadataQualifiedName] = ((IItem)item).GetMetadataValueEscaped(metadataName);
                    }
                    catch (InvalidOperationException e)
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation,
                            "CannotEvaluateItemMetadata", metadataName, e.Message);
                    }
                }
            }

            return itemMetadataValues;
        }

        /// <summary>
        /// Partitions items of a single item type into batches of items that have the same values for all the item
        /// metadata consumed by the batchable object, like <see cref="BucketConsumedItems"/> does into buckets.
        /// </summary>
        private static List<ItemBatch> BatchItems(
            ICollection<ProjectItemInstance> items,
            Dictionary<string, MetadataReference> consumedMetadataReferences,
            ElementLocation elementLocation)
        {
            var batches = new List<ItemBatch>();
            Dictionary<Dictionary<string, string>, int> batchIndexes = new(MetadataValuesComparer.Instance);

            foreach (ProjectItemInstance item in items)
            {
                Dictionary<string, string> itemMetadataValues = GetItemMetadataValues(item, consumedMetadataReferences, elementLocation);

                if (batchIndexes.TryGetValue(itemMetadataValues, out int batchIndex))
                {
                    batches[batchIndex].Items.Add(item);
                }
                else
                {
                    batchIndexes.Add(itemMetadataValues, batches.Count);
                    batches.Add(new ItemBatch(itemMetadataValues, [item]));
                }
            }

            return batches;
        }

        /// <summary>
        /// Items of a single item type that have the same values for all the item metadata consumed by a batchable object.
        /// </summary>
        internal readonly struct ItemBatch
        {
            internal ItemBatch(Dictionary<string, string> metadata, List<ProjectItemInstance> items)
            {
                Metadata = metadata;
                Items = items;
            }

            /// <summary>
            /// The values of the consumed metadata, keyed by their possibly qualified names, as for an <see cref="ItemBucket"/>.
            /// </summary>
            internal Dictionary<string, string> Metadata { get; }

            /// <summary>
            /// The items, in the order in which they were declared.
            /// </summary>
            internal List<ProjectItemInstance> Items { get; }
        }

        /// <summary>
        /// Compares the values of the metadata consumed by a batchable object for two items. Items share a bucket
        /// when every value matches ignoring case.
        /// </summary>
        private sealed class MetadataValuesComparer : IEqualityComparer<Dictionary<string, string>>
        {
            internal static readonly MetadataValuesComparer Instance = new();

            public bool Equals(Dictionary<string, string> x, Dictionary<string, string> y)
                => HashTableUtility.Compare(x, y) == 0;

            public int GetHashCode(Dictionary<string, string> metadataValues)
            {
                // Independent of the order in which the values were added.
                int hashCode = 0;
                foreach (KeyValuePair<string, string> metadataValue in metadataValues)
                {
                    hashCode = unchecked(hashCode + (MSBuildNameIgnoreCaseComparer.Default.GetHashCode(metadataValue.Key)
                        ^ (metadataValue.Value is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(metadataValue.Value))));
                }

                return hashCode;
            }
        }

        #endregion
    }
}
