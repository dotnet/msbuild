// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

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
