// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Abstract base class for cache entries
    /// </summary>
    internal abstract class CacheEntry
    {
        #region Constructors

        /// <summary>
        /// Default constructor
        /// </summary>
        protected CacheEntry()
        {
        }

        /// <summary>
        /// Public constructor
        /// </summary>
        /// <param name="name"></param>
        protected CacheEntry(string name)
        {
            this.name = name;
        }

        #endregion

        #region Properties

        private string name;

        /// <summary>
        /// Name of the cache entry
        /// </summary>
        internal string Name
        {
            get { return name; }
            set { name = value; }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Returns true if the given cache entry contains equivalent contents
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        internal abstract bool IsEquivalent(CacheEntry other);

        #endregion

        #region CustomSerializationToStream

        internal virtual void WriteToStream(BinaryWriter writer)
        {
            if (name == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(name);
            }
        }

        internal virtual void CreateFromStream(BinaryReader reader)
        {
            if (reader.ReadByte() == 0)
            {
                name = null;
            }
            else
            {
                name = reader.ReadString();
            }
        }

        #endregion

    }
}
