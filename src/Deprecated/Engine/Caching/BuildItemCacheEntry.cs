// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// A cache entry holding an array of build items
    /// </summary>
    internal class BuildItemCacheEntry : CacheEntry
    {
        #region Constructors

        /// <summary>
        /// Default constructor
        /// </summary>
        internal BuildItemCacheEntry()
        {
        }

        /// <summary>
        /// Public constructor
        /// </summary>
        /// <param name="name"></param>
        /// <param name="taskItems"></param>
        internal BuildItemCacheEntry(string name, BuildItem[] buildItems)
            : base(name)
        {
            this.buildItems = buildItems;
        }

        #endregion

        #region Properties

        private BuildItem[] buildItems;

        /// <summary>
        /// Task items held by this cache entry
        /// </summary>
        internal BuildItem[] BuildItems
        {
            get { return buildItems; }
            set { buildItems = value; }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Returns true if the given cache entry contains equivalent contents
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        internal override bool IsEquivalent(CacheEntry other)
        {
            if ((other == null) || (other.GetType() != this.GetType()))
            {
                return false;
            }

            BuildItemCacheEntry otherEntry = (BuildItemCacheEntry)other;

            if (this.Name != otherEntry.Name)
            {
                return false;
            }

            if ((this.BuildItems == null && otherEntry.BuildItems != null) ||
                (this.BuildItems != null && otherEntry.BuildItems == null))
            {
                return false;
            }

            if ((this.BuildItems == null) && (otherEntry.BuildItems == null))
            {
                return true;
            }

            if (this.BuildItems.Length != otherEntry.BuildItems.Length)
            {
                return false;
            }

            for (int i = 0; i < this.BuildItems.Length; i++)
            {
                if ((this.BuildItems[i] == null && otherEntry.BuildItems[i] != null) ||
                    (this.BuildItems[i] != null && otherEntry.BuildItems[i] == null))
                {
                    return false;
                }

                if ((this.BuildItems[i].FinalItemSpecEscaped != otherEntry.BuildItems[i].FinalItemSpecEscaped) ||
                    (this.BuildItems[i].GetCustomMetadataCount() != otherEntry.BuildItems[i].GetCustomMetadataCount()))
                {
                    return false;
                }

                HashSet<string> otherEntryMetadataNames = new HashSet<string>(otherEntry.BuildItems[i].GetAllCustomMetadataNames(), StringComparer.Ordinal);

                foreach (string metadataName in this.BuildItems[i].GetAllCustomMetadataNames())
                {
                    if ((!otherEntryMetadataNames.Contains(metadataName)) ||
                        (this.BuildItems[i].GetEvaluatedMetadataEscaped(metadataName) != otherEntry.BuildItems[i].GetEvaluatedMetadataEscaped(metadataName)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        #endregion

        #region CustomSerializationToStream
        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);
            if (buildItems == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write((Int32)buildItems.Length);
                foreach (BuildItem item in buildItems)
                {
                    if (item == null)
                    {
                        writer.Write((byte)0);
                    }
                    writer.Write((byte)1);
                    item.WriteToStream(writer);
                }
            }
        }

        internal override void CreateFromStream(BinaryReader reader)
        {
            base.CreateFromStream(reader);
            buildItems = null;
            if (reader.ReadByte() != 0)
            {
                int sizeOfArray = reader.ReadInt32();
                buildItems = new BuildItem[sizeOfArray];
                for (int j = 0; j < sizeOfArray; j++)
                {
                    BuildItem itemToAdd = null;
                    if (reader.ReadByte() != 0)
                    {
                        itemToAdd = new BuildItem(null, string.Empty);
                        itemToAdd.CreateFromStream(reader);
                    }
                    buildItems[j] = itemToAdd;
                }
            }
        }
        #endregion
    }
}
