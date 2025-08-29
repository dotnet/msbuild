// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

using System;
using System.Xml;
using System.Collections;

using Microsoft.Build.BuildEngine.Shared;
using System.Collections.Generic;

namespace Microsoft.Build.BuildEngine
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
    /// same name on a different item type. For example, %(a.m) is distinct to %(b.m), and items of type �b� 
    /// are considered to always have a blank value for %(a.m). This means items of type �b� will only be 
    /// placed in buckets where %(a.m) is blank. However %(a.m) is equivalent to %(m) on items of type �a�.
    /// 
    /// There is an extra ambiguity rule: every items consumed by the object must have an explicit value for 
    /// every piece of unqualified metadata. For example, if @(a), %(m), and %(a.n) are consumed, every item 
    /// of type �a� must have a value for the metadata �m� but need not all necessarily have a value for the 
    /// metadata �n�. This rule eliminates ambiguity about whether items that do not define values for an 
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
        /// <returns>ArrayList containing ItemBucket objects, each one representing an execution batch.</returns>
        internal static ArrayList PrepareBatchingBuckets
        (
            XmlNode parentNode,
            List<string> batchableObjectParameters,
            Lookup lookup
        )
        {
            return PrepareBatchingBuckets(parentNode, batchableObjectParameters, lookup, null);
        }

        /// <summary>
        /// Determines how many times the batchable object needs to be executed (each execution is termed a "batch"), and prepares
        /// buckets of items to pass to the object in each batch.
        /// </summary>
        /// <param name="parentNode"></param>
        /// <param name="batchableObjectParameters"></param>
        /// <param name="lookup"></param>
        /// <param name="implicitBatchableItemType">Any item type that can be considered an implicit input to this batchable object.
        /// This is useful for items inside targets, where the item name is plainly an item type that's an "input" to the object.</param>
        /// <returns>ArrayList containing ItemBucket objects, each one representing an execution batch.</returns>
        internal static ArrayList PrepareBatchingBuckets
        (
            XmlNode parentNode,
            List<string> batchableObjectParameters,
            Lookup lookup,
            string implicitBatchableItemType
        )
        {
            ErrorUtilities.VerifyThrow(parentNode != null, "Need the XML node that represents the batchable object.");
            ErrorUtilities.VerifyThrow(batchableObjectParameters != null, "Need the parameters of the batchable object to determine if it can be batched.");
            ErrorUtilities.VerifyThrow(lookup != null, "Need to specify the lookup.");

            ItemsAndMetadataPair pair = ExpressionShredder.GetReferencedItemNamesAndMetadata(batchableObjectParameters);

            // All the @(itemname) item list references in the tag, including transforms, etc.
            // The keys in the hashtable are the item names, and the values are all String.Empty (not used).            
            Hashtable consumedItemReferences = pair.Items;

            // All the %(itemname.metadataname) references in the tag (not counting those embedded 
            // inside item transforms), and note that the itemname portion is optional.
            // The keys in the returned hash table are the qualified metadata names (e.g. "EmbeddedResource.Culture"
            // or just "Culture").  The values are MetadataReference structs, which simply split out the item 
            // name (possibly null) and the actual metadata name.            
            Dictionary<string, MetadataReference> consumedMetadataReferences = pair.Metadata;

            ArrayList buckets = null;
            if (consumedMetadataReferences?.Count > 0)
            {
                // Add any item types that we were explicitly told to assume.
                if (implicitBatchableItemType != null)
                {
                    consumedItemReferences = Utilities.CreateTableIfNecessary(consumedItemReferences);
                    consumedItemReferences[implicitBatchableItemType] = String.Empty;
                }

                // This method goes through all the item list references and figures out which ones
                // will be participating in batching, and which ones won't.  We get back a hashtable
                // where the key is the item name that will be participating in batching.  The values
                // are all String.Empty (not used).  This method may return additional item names 
                // that weren't represented in "consumedItemReferences"... this would happen if there
                // were qualified metadata references in the consumedMetadataReferences table, such as 
                // %(EmbeddedResource.Culture).
                Hashtable itemListsToBeBatched = GetItemListsToBeBatched(parentNode, consumedMetadataReferences, consumedItemReferences, lookup);

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
                        ProjectErrorUtilities.VerifyThrowInvalidProject(false,
                            parentNode, "CannotReferenceItemMetadataWithoutItemName", unqualifiedMetadataName);
                    }
                }
                else
                {
                    // If the batchable object consumes item metadata as well as items to be batched,
                    // we need to partition the items consumed by the object.
                    buckets = BucketConsumedItems(parentNode, lookup, itemListsToBeBatched, consumedMetadataReferences);
                }
            }

            // if the batchable object does not consume any item metadata or items, or if the item lists it consumes are all
            // empty, then the object does not need to be batched
            if ((buckets == null) || (buckets.Count == 0))
            {
                // create a default bucket that references the project items and properties -- this way we always have a bucket
                buckets = new ArrayList(1);
                buckets.Add(new ItemBucket(null, null, lookup, buckets.Count));
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
        /// <returns>Hashtable containing the item names that should be batched.</returns>
        private static Hashtable GetItemListsToBeBatched
        (
            XmlNode parentNode,
            Dictionary<string, MetadataReference> consumedMetadataReferences,   // Key is [string] potentially qualified metadata name
                                                                                // Value is [struct MetadataReference]
            Hashtable consumedItemReferenceNames,       // Key is [string] item name.
                                                        // Value is always String.Empty (unused).
            Lookup lookup
        )
        {
            // The keys in this hashtable are the names of the items that we will batch on.
            // The values are always String.Empty (not used).
            Hashtable itemListsToBeBatched = new Hashtable(StringComparer.OrdinalIgnoreCase);

            // Loop through all the metadata references and find the ones that are qualified
            // with an item name.
            foreach (MetadataReference consumedMetadataReference in consumedMetadataReferences.Values)
            {
                if (consumedMetadataReference.itemName != null)
                {
                    // Rule #1.  Qualified metadata reference.
                    // For metadata references that are qualified with an item name 
                    // (e.g., %(EmbeddedResource.Culture) ), we add that item name to the list of 
                    // consumed item names, even if the item name wasn't otherwise referenced via
                    // @(...) syntax, and even if every item in the list doesn't necessary contain
                    // a value for this metadata.  This is the special power that you get by qualifying 
                    // the metadata reference with an item name.
                    itemListsToBeBatched[consumedMetadataReference.itemName] = String.Empty;

                    // Also add this qualified item to the consumed item references list, because
                    // %(EmbeddedResource.Culture) effectively means that @(EmbeddedResource) is
                    // being consumed, even though we may not see literally "@(EmbeddedResource)"
                    // in the tag anywhere.  Adding it to this list allows us (down below in this
                    // method) to check that every item in this list has a value for each 
                    // unqualified metadata reference.
                    consumedItemReferenceNames = Utilities.CreateTableIfNecessary(consumedItemReferenceNames);
                    consumedItemReferenceNames[consumedMetadataReference.itemName] = String.Empty;
                }
            }

            // Loop through all the metadata references and find the ones that are unqualified.
            foreach (MetadataReference consumedMetadataReference in consumedMetadataReferences.Values)
            {
                if (consumedMetadataReference.itemName == null)
                {
                    // Rule #2.  Unqualified metadata reference.
                    // For metadata references that are unqualified, every single consumed item
                    // must contain a value for that metadata.  If any item doesn't, it's an error
                    // to use unqualified metadata.
                    if (consumedItemReferenceNames != null)
                    {
                        foreach (string consumedItemName in consumedItemReferenceNames.Keys)
                        {
                            // Loop through all the items in the item list.
                            BuildItemGroup items = lookup.GetItems(consumedItemName);

                            if (items != null)
                            {
                                // Loop through all the items in the BuildItemGroup.
                                foreach (BuildItem item in items)
                                {
                                    ProjectErrorUtilities.VerifyThrowInvalidProject(
                                        item.HasMetadata(consumedMetadataReference.metadataName),
                                        parentNode, "ItemDoesNotContainValueForUnqualifiedMetadata",
                                        item.Include, consumedItemName, consumedMetadataReference.metadataName);
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
        /// PERF NOTE: Given n items and m batching metadata that produce l buckets, it is usually the case that n > l > m,
        /// because a batchable object typically uses one or two item metadata to control batching, and only has a handful of
        /// buckets. The number of buckets is typically only large if a batchable object is using single-item batching
        /// (where l == n). Any algorithm devised for bucketing therefore, should try to minimize n and l in its complexity
        /// equation. The algorithm below has a complexity of O(n*lg(l)*m/2) in its comparisons, and is effectively O(n) when
        /// l is small, and O(n*lg(n)) in the worst case as l -> n. However, note that the comparison complexity is not the
        /// same as the operational complexity for this algorithm. The operational complexity of this algorithm is actually
        /// O(n*m + n*lg(l)*m/2 + n*l/2 + n + l), which is effectively O(n^2) in the worst case. The additional complexity comes
        /// from the array and metadata operations that are performed. However, those operations are extremely cheap compared
        /// to the comparison operations, which dominate the time spent in this method.
        /// </remarks>
        /// <returns>ArrayList containing ItemBucket objects (can be empty), each one representing an execution batch.</returns>
        private static ArrayList BucketConsumedItems
        (
            XmlNode parentNode,
            Lookup lookup,
            Hashtable itemListsToBeBatched,
            Dictionary<string, MetadataReference> consumedMetadataReferences
        )
        {
            ErrorUtilities.VerifyThrow(itemListsToBeBatched.Count > 0, "Need item types consumed by the batchable object.");
            ErrorUtilities.VerifyThrow(consumedMetadataReferences.Count > 0, "Need item metadata consumed by the batchable object.");

            ArrayList buckets = new ArrayList();

            // Get and iterate through the list of item names that we're supposed to batch on.
            foreach (DictionaryEntry entry in itemListsToBeBatched)
            {
                string itemName = (string)entry.Key;

                // Use the previously-fetched items, if possible
                BuildItemGroup items;
                if (entry.Value is BuildItemGroup)
                {
                    items = (BuildItemGroup)entry.Value;
                }
                else
                {
                    items = lookup.GetItems(itemName);
                }

                if (items != null)
                {
                    foreach (BuildItem item in items)
                    {
                        // Get this item's values for all the metadata consumed by the batchable object.
                        Dictionary<string, string> itemMetadataValues = GetItemMetadataValues(parentNode, item, consumedMetadataReferences);

                        // put the metadata into a dummy bucket we can use for searching
                        ItemBucket dummyBucket = ItemBucket.GetDummyBucketForComparisons(itemMetadataValues);

                        // look through all previously created buckets to find a bucket whose items have the same values as
                        // this item for all metadata consumed by the batchable object
                        int matchingBucketIndex = buckets.BinarySearch(dummyBucket);

                        ItemBucket matchingBucket = (matchingBucketIndex >= 0)
                            ? (ItemBucket)buckets[matchingBucketIndex]
                            : null;

                        // If we didn't find a bucket that matches this item, create a new one, adding
                        // this item to the bucket.
                        if (matchingBucket == null)
                        {
                            matchingBucket = new ItemBucket(itemListsToBeBatched.Keys, itemMetadataValues, lookup, buckets.Count);

                            // make sure to put the new bucket into the appropriate location
                            // in the sorted list as indicated by the binary search
                            // NOTE: observe the ~ operator (bitwise complement) in front of
                            // the index -- see MSDN for more information on the return value
                            // from the ArrayList.BinarySearch() method
                            buckets.Insert(~matchingBucketIndex, matchingBucket);
                        }

                        // We already have a bucket for this type of item, so add this item to
                        // the bucket.
                        matchingBucket.AddItem(item);
                    }
                }
            }

            // Put the buckets back in the order in which they were discovered, so that the first
            // item declared in the project file ends up in the first batch passed into the target/task.
            ArrayList orderedBuckets = ArrayList.Repeat(null, buckets.Count);
            foreach (ItemBucket bucket in buckets)
            {
                orderedBuckets[bucket.BucketSequenceNumber] = bucket;
            }
            return orderedBuckets;
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
        private static Dictionary<string, string> GetItemMetadataValues
        (
            XmlNode parentNode,
            BuildItem item,
            Dictionary<string, MetadataReference> consumedMetadataReferences
        )
        {
            Dictionary<string, string> itemMetadataValues = new Dictionary<string, string>(consumedMetadataReferences.Count, StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, MetadataReference> consumedMetadataReference in consumedMetadataReferences)
            {
                string metadataQualifiedName = consumedMetadataReference.Key;
                string metadataItemName = consumedMetadataReference.Value.itemName;
                string metadataName = consumedMetadataReference.Value.metadataName;

                if (
                        (metadataItemName != null) &&
                        (!String.Equals(item.Name, metadataItemName, StringComparison.OrdinalIgnoreCase))
                    )
                {
                    itemMetadataValues[metadataQualifiedName] = String.Empty;
                }
                else
                {
                    try
                    {
                        itemMetadataValues[metadataQualifiedName] = item.GetEvaluatedMetadataEscaped(metadataName);
                    }
                    catch (InvalidOperationException e)
                    {
                        ProjectErrorUtilities.VerifyThrowInvalidProject(false, parentNode,
                            "CannotEvaluateItemMetadata", metadataName, e.Message);
                    }
                }
            }

            return itemMetadataValues;
        }

        #endregion
    }
}
