// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Security.Permissions;
using System.Diagnostics;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{

    /// <summary>
    /// A hashtable wrapper that defers copying until the data is written.
    /// </summary>
    internal sealed class CopyOnWriteHashtable : IDictionary, ICloneable
    {
        // Either of these can act as the true backing writeableData.
        private Hashtable writeableData = null;

        // These are references to someone else's backing writeableData.
        private Hashtable readonlyData = null;

        // This is used to synchronize access to the readonlyData and writeableData fields.
        private object sharedLock;

        // Carry around the StringComparer when possible to make Clear less expensive.
        StringComparer stringComparer = null;

#region Construct
        /// <summary>
        /// Construct as a traditional data-backed hashtable.
        /// </summary>
        /// <param name="stringComparer"></param>
        internal CopyOnWriteHashtable(StringComparer stringComparer)
            : this(0, stringComparer)
        {
            this.sharedLock = new object();
        }

        /// <summary>
        /// Construct with specified initial capacity. If the capacity is known
        /// up front, specifying it avoids unnecessary rehashing operations
        /// </summary>
        internal CopyOnWriteHashtable(int capacity, StringComparer stringComparer)
        {
            ErrorUtilities.VerifyThrowArgumentNull(stringComparer, "stringComparer");
            this.sharedLock = new object();

            if (capacity == 0)
            {
                // Actually 0 tells the Hashtable to use its default, but let's not rely on that
                writeableData = new Hashtable(stringComparer);
            }
            else
            {
                writeableData = new Hashtable(capacity, stringComparer);
            }
            readonlyData = null;
            this.stringComparer = stringComparer;
        }

        /// <summary>
        /// Construct over an IDictionary instance.
        /// </summary>
        /// <param name="dictionary"></param>
        /// <param name="stringComparer">The string comparer to use.</param>
        internal CopyOnWriteHashtable(IDictionary dictionary, StringComparer stringComparer)
        {
            ErrorUtilities.VerifyThrowArgumentNull(dictionary, "dictionary");
            ErrorUtilities.VerifyThrowArgumentNull(stringComparer, "stringComparer");

            this.sharedLock = new object();
            CopyOnWriteHashtable source = dictionary as CopyOnWriteHashtable;
            if (source != null)
            {
                if (source.stringComparer.GetHashCode() == stringComparer.GetHashCode())
                {
                    // If we're copying another CopyOnWriteHashtable then we can defer the clone until later.
                    ConstructFrom(source);
                    return;
                }
                else
                {
                    // Technically, it would be legal to fall through here and let a new hashtable be constructed.
                    // However, Engine is supposed to use consistent case comparisons everywhere and so, for us,
                    // this means a bug in the engine code somewhere.
                    throw new InternalErrorException("Bug: Changing the case-sensitiveness of a copied hash-table.");
                }

            }

            // Can't defer this because we don't control what gets written to the dictionary exogenously.
            writeableData = new Hashtable(dictionary, stringComparer);
            readonlyData = null;
            this.stringComparer = stringComparer;
        }

        /// <summary>
        /// Construct a shallow copy over another instance of this class.
        /// </summary>
        /// <param name="that"></param>
        private CopyOnWriteHashtable(CopyOnWriteHashtable that)
        {
            this.sharedLock = new object();
            ConstructFrom(that);
        }
        
        /// <summary>
        /// Implementation of construction logic.
        /// </summary>
        /// <param name="that"></param>
        private void ConstructFrom(CopyOnWriteHashtable that)
        {
            lock (that.sharedLock)
            {
                this.writeableData = null;

                // If the source it was writeable, need to transform it into 
                // read-only because we don't want subsequent writes to bleed through.
                if (that.writeableData != null)
                {
                    that.readonlyData = that.writeableData;
                    that.writeableData = null;
                }

                this.readonlyData = that.readonlyData;
                this.stringComparer = that.stringComparer;
            }
        }

        /// <summary>
        /// Whether or not this CopyOnWriteHashtable is currently a shallow or deep copy.
        /// This state can change from true->false when this hashtable is written to.
        /// </summary>
        internal bool IsShallowCopy
        {
            get
            {
                return this.readonlyData != null;
            }
        }
#endregion
#region Pass-through Hashtable methods.
        public bool Contains(Object key) {return ReadOperation.Contains(key);}
        public void Add(Object key, Object value) {WriteOperation.Add(key, value);}
        public void Clear() 
        {
            lock (sharedLock)
            {
                ErrorUtilities.VerifyThrow(stringComparer != null, "Should have a valid string comparer.");

                writeableData = new Hashtable(stringComparer);
                readonlyData = null;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() { return ((IEnumerable)ReadOperation).GetEnumerator(); }
        public IDictionaryEnumerator GetEnumerator() {return ReadOperation.GetEnumerator();}
        public void Remove(Object key) {WriteOperation.Remove(key);}        
        public bool IsFixedSize { get { return ReadOperation.IsFixedSize; }}
        public bool IsReadOnly {get {return ReadOperation.IsFixedSize;}}
        public ICollection Keys {get {return ReadOperation.Keys;}}
        public ICollection Values {get {return ReadOperation.Values;}}
        public void CopyTo(Array array, int arrayIndex) { ReadOperation.CopyTo(array, arrayIndex); }
        public int Count{get { return ReadOperation.Count; }}
        public bool IsSynchronized {get { return ReadOperation.IsSynchronized; }}
        public Object SyncRoot {get { return ReadOperation.SyncRoot; }}
        public bool ContainsKey(Object key)    {return ReadOperation.Contains(key);}
        
        public Object this[Object key] 
        {
            get 
            {
                return ReadOperation[key];
            }
            set 
            {
                lock (sharedLock)
                {
                    if(writeableData != null)
                    {
                        writeableData[key] = value;
                    }
                    else
                    {
                        // Setting to exactly the same value? Skip the the Clone in this case.
                        if (readonlyData[key] != value || (!readonlyData.ContainsKey(key)))
                        {
                            WriteOperation[key] = value;
                        }
                    }
                }

            }
        }
#endregion

        /// <summary>
        /// Clone this.
        /// </summary>
        /// <returns></returns>
        public Object Clone()
        {
            return new CopyOnWriteHashtable(this);
        }

        /// <summary>
        /// Returns a hashtable instance for reading from.
        /// </summary>
        private Hashtable ReadOperation
        {
            get
            {
                lock (sharedLock)
                {
                    if (readonlyData != null)
                    {
                        return readonlyData;
                    }

                    return writeableData;
                }
            }
        }

        /// <summary>
        /// Returns a hashtable instance for writting to.
        /// Clones the readonly hashtable if necessary to create a writeable version.
        /// </summary>
        private Hashtable WriteOperation
        {
            get
            {
                lock (sharedLock)
                {
                    if (writeableData == null)
                    {
                        writeableData = (Hashtable)readonlyData.Clone();
                        readonlyData = null;
                    }

                    return writeableData;
                }
            }
        }
    }
}
