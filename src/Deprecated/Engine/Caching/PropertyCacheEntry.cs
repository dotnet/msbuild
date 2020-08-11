// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// A cache entry holding a name-value pair
    /// </summary>
    internal class PropertyCacheEntry : CacheEntry
    {
        #region Constructors

        /// <summary>
        /// Default constructor
        /// </summary>
        internal PropertyCacheEntry()
        {
        }

        /// <summary>
        /// Public constructor
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        internal PropertyCacheEntry(string name, string value)
            : base(name)
        {
            this.value = value;
        }

        #endregion

        #region Properties

        private string value;

        /// <summary>
        /// String value held by this cache entry
        /// </summary>
        internal string Value
        {
            get { return this.value; }
            set { this.value = value; }
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

            PropertyCacheEntry otherEntry = (PropertyCacheEntry)other;

            if (this.Name != otherEntry.Name)
            {
                return false;
            }

            return this.Value == otherEntry.Value;
        }

        #region CustomSerializationToStream
        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);

            if (value == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(value);
            }
        }

        internal override void CreateFromStream(BinaryReader reader)
        {
            base.CreateFromStream(reader);

            if (reader.ReadByte() == 0)
            {
                value = null;
            }
            else
            {
                value = reader.ReadString();
            }
        }
        #endregion

        #endregion
    }
}
