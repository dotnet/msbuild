// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Implementation of ICollection&lt;Toolset&gt; that also supports
    /// key-based retrieval by passing the string value of the tools version
    /// corresponding with the desired Toolset.
    /// NOTE: This collection does not support ICollection&lt;Toolset&gt;'s
    /// Remove or Clear methods, and calls to these will generate exceptions.
    /// </summary>
    public class ToolsetCollection : ICollection<Toolset>
    {
        // the parent engine 
        private Engine parentEngine = null;

        // underlying map keyed off toolsVersion
        private Dictionary<string, Toolset> toolsetMap = null;

        /// <summary>
        /// Private default Ctor. Other classes should not be constructing 
        /// instances of this class without providing an Engine object.
        /// </summary>
        private ToolsetCollection()
        {
        }

        /// <summary>
        /// Ctor.  This is the only Ctor accessible to other classes.
        /// Third parties should not be creating instances of this class;
        /// instead, they should query an Engine object for one.
        /// </summary>
        /// <param name="parentEngine"></param>
        internal ToolsetCollection(Engine parentEngine)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parentEngine, "parentEngine");

            this.parentEngine = parentEngine;
            this.toolsetMap = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The names of the toolsets stored in this collection.
        /// </summary>
        public IEnumerable<string> ToolsVersions
        {
            get
            {
                return toolsetMap.Keys;
            }
        }

        /// <summary>
        /// Gets the Toolset with matching toolsVersion.
        /// </summary>
        /// <param name="toolsVersion"></param>
        /// <returns>Toolset with matching toolsVersion, or null if none exists.</returns>
        public Toolset this[string toolsVersion]
        {
            get
            {
                ErrorUtilities.VerifyThrowArgumentLength(toolsVersion, "toolsVersion");

                if (this.toolsetMap.ContainsKey(toolsVersion))
                {
                    // Return clone of toolset so caller can't modify properties on it
                    return this.toolsetMap[toolsVersion].Clone();
                }

                return null;
            }
        }

        /// <summary>
        /// Determines whether the collection contains a Toolset with matching
        /// tools version.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Contains(string toolsVersion)
        {
            return toolsetMap.ContainsKey(toolsVersion);
        }

        #region ICollection<Toolset> Implementations

        /// <summary>
        /// Count of elements in this collection.
        /// </summary>
        public int Count
        {
            get
            {
                return toolsetMap.Count;
            }
        }

        /// <summary>
        /// Always returns false
        /// </summary>
        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Adds the given Toolset to this collection, replacing any previous value
        /// with the same tools version.  Also notifies the parent Engine of the
        /// change.
        /// </summary>
        /// <param name="item"></param>
        public void Add(Toolset item)
        {
            ErrorUtilities.VerifyThrowArgumentNull(item, "item");

            if (toolsetMap.ContainsKey(item.ToolsVersion))
            {
                // It already exists: replace it with the new toolset
                toolsetMap[item.ToolsVersion] = item;
            }
            else
            {
                toolsetMap.Add(item.ToolsVersion,item);
            }

            // The parent engine needs to handle this as well
            parentEngine.AddToolset(item);
        }

        /// <summary>
        /// This method is not supported.
        /// </summary>
        public void Clear()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Determines whether or not this collection contains the given toolset.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Contains(Toolset item)
        {
            return toolsetMap.ContainsValue(item);
        }

        /// <summary>
        /// Copies the contents of this collection to the given array, beginning
        /// at the given index.
        /// </summary>
        /// <param name="array"></param>
        /// <param name="arrayIndex"></param>
        public void CopyTo(Toolset[] array, int arrayIndex)
        {
            toolsetMap.Values.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Generic enumerator for the Toolsets in this collection.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<Toolset> GetEnumerator()
        {
            return this.toolsetMap.Values.GetEnumerator();
        }

        /// <summary>
        /// Non-generic enumerator for the Toolsets in this collection.
        /// </summary>
        /// <returns></returns>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// This method is not supported.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Remove(Toolset item)
        {
            throw new NotSupportedException();
        }

        #endregion
    }
}
