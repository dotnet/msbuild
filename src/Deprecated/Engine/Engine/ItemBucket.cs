// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;

using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class represents a collection of items that are homogeneous w.r.t.
    /// a certain set of metadata.
    /// </summary>
    /// <owner>SumedhK</owner>
    internal sealed class ItemBucket : IComparable
    {
        #region Member data

        /// <summary>
        /// This single object contains all of the data necessary to perform expansion of metadata, properties,
        /// and items.
        /// </summary>
        private Expander expander;

        /// <summary>
        /// The items for this bucket.
        /// </summary>
        private Lookup lookup;

        /// <summary>
        /// When buckets are being created for batching purposes, this indicates which order the 
        /// buckets were created in, so that the target/task being batched gets called with the items
        /// in the same order as they were declared in the project file.  For example, the first
        /// bucket created gets bucketSequenceNumber=0, the second bucket created gets 
        /// bucketSequenceNumber=1, etc.
        /// </summary>
        private int bucketSequenceNumber;

        #endregion

        #region Constructors

        /// <summary>
        /// Private default constructor disallows parameterless instantiation.
        /// </summary>
        /// <owner>SumedhK</owner>
        private ItemBucket()
        {
            // do nothing
        }

        /// <summary>
        /// Creates an instance of this class using the given bucket data.
        /// </summary>
        /// <param name="itemNames">Item types being batched on: null indicates no batching is occurring</param>
        /// <param name="itemMetadata">Hashtable of item metadata values: null indicates no batching is occurring</param>
        internal ItemBucket
        (
            ICollection itemNames,
            Dictionary<string, string> itemMetadata,
            Lookup lookup,
            int bucketSequenceNumber
        )
        {
            ErrorUtilities.VerifyThrow(lookup != null, "Need lookup.");

            // Create our own lookup just for this bucket
            this.lookup = lookup.Clone();

            // Push down the items, so that item changes in this batch are not visible to parallel batches
            this.lookup.EnterScope();

            // Add empty item groups for each of the item names, so that (unless items are added to this bucket) there are
            // no item types visible in this bucket among the item types being batched on
            if (itemNames != null)
            {
                foreach (string name in itemNames)
                {
                    this.lookup.PopulateWithItems(name, new BuildItemGroup());
                }
            }

            this.expander = new Expander(this.lookup.ReadOnlyLookup, itemMetadata);
            this.bucketSequenceNumber = bucketSequenceNumber;
        }

        #endregion

        #region Comparison methods

        /// <summary>
        /// Compares this item bucket against the given one. The comparison is
        /// solely based on the values of the item metadata in the buckets.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="obj"></param>
        /// <returns>
        /// -1, if this bucket is "less than" the second one
        ///  0, if this bucket is equivalent to the second one
        /// +1, if this bucket is "greater than" the second one
        /// </returns>
        public int CompareTo(object obj)
        {
            return HashTableUtility.Compare(this.Expander.ItemMetadata, ((ItemBucket)obj).Expander.ItemMetadata);
        }

        /// <summary>
        /// Constructs a token bucket object that can be compared against other
        /// buckets. This dummy bucket is a patently invalid bucket, and cannot
        /// be used for any other operations besides comparison.
        /// </summary>
        /// <remarks>
        /// PERF NOTE: A dummy bucket is intentionally very light-weight, and it
        /// allocates a minimum of memory compared to a real bucket.
        /// </remarks>
        /// <owner>SumedhK</owner>
        /// <param name="itemMetadata"></param>
        /// <returns>An item bucket that is invalid for everything except comparisons.</returns>
        internal static ItemBucket GetDummyBucketForComparisons(Dictionary<string, string> itemMetadata)
        {
            ItemBucket bucket = new ItemBucket();
            bucket.expander = new Expander((ReadOnlyLookup)null, itemMetadata);

            return bucket;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the object that knows how to handle all kinds of expansion for this bucket.
        /// </summary>
        /// <owner>RGoel</owner>
        internal Expander Expander
        {
            get
            {
                return this.expander;
            }
        }

        /// <summary>
        /// When buckets are being created for batching purposes, this indicates which order the 
        /// buckets were created in, so that the target/task being batched gets called with the items
        /// in the same order as they were declared in the project file.  For example, the first
        /// bucket created gets bucketSequenceNumber=0, the second bucket created gets 
        /// bucketSequenceNumber=1, etc.
        /// </summary>
        /// <owner>RGoel</owner>
        internal int BucketSequenceNumber
        {
            get
            {
                return this.bucketSequenceNumber;
            }
        }

        /// <summary>
        /// The items for this bucket.
        /// </summary>
        internal Lookup Lookup
        {
            get
            {
                return this.lookup;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Adds a new item to this bucket.
        /// </summary>
        internal void AddItem(BuildItem item)
        {
            this.lookup.PopulateWithItem(item);
        }

        #endregion
    }
}
