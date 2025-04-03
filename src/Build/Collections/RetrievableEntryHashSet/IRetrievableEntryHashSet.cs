// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Runtime.Serialization;

#nullable disable

namespace Microsoft.Build.Collections
{
    internal interface IRetrievableEntryHashSet<T> :
        ICollection<T>,
        ISerializable,
        IDeserializationCallback,
        IDictionary<string, T>
        where T : class, IKeyed
    {
        /// <summary>
        /// Gets the item with the given name.
        /// </summary>
        /// <param name="key">key to check for containment.</param>
        /// <returns>The item (if contained).</returns>
        /// <exception cref="KeyNotFoundException">Thrown if no item with the given name is present in the collection.</exception>
        T Get(string key);

        /// <summary>
        /// Gets the item if any with the given name.
        /// </summary>
        /// <param name="key">key to check for containment.</param>
        /// <param name="index">The position of the substring within <paramref name="key"/>.</param>
        /// <param name="length">The maximum number of characters in the <paramref name="key"/> to lookup.</param>
        /// <returns>The item (if contained).</returns>
        /// <exception cref="KeyNotFoundException">Thrown if no item with the given name is present in the collection.</exception>
        T Get(string key, int index, int length);

        /// <summary>
        /// Copies the contents of this HashSet into the provided array.
        /// </summary>
        /// <param name="array">The array into which the contents of this HashSet will be copied.</param>
        void CopyTo(T[] array);

        /// <summary>
        /// Copies the contents of this HashSet into the provided array.
        /// </summary>
        /// <param name="array">The array into which the contents of this HashSet will be copied.</param>
        /// <param name="arrayIndex">The index within <paramref name="array"/> where the first item will be placed.</param>
        /// <param name="count">The number of items from HashSet to copy into <paramref name="array"/>.</param>
        void CopyTo(T[] array, int arrayIndex, int count);

        /// <summary>
        /// Take the union of this HashSet with other. Modifies this set.
        /// 
        /// Implementation note: GetSuggestedCapacity (to increase capacity in advance avoiding 
        /// multiple resizes ended up not being useful in practice; quickly gets to the 
        /// point where it's a wasteful check.
        /// </summary>
        /// <param name="other">enumerable with items to add</param>
        void UnionWith(IEnumerable<T> other);

        /// <summary>
        /// Sets the capacity of this list to the size of the list (rounded up to nearest prime),
        /// unless count is 0, in which case we release references.
        /// 
        /// This method can be used to minimize a list's memory overhead once it is known that no
        /// new elements will be added to the list. To completely clear a list and release all 
        /// memory referenced by the list, execute the following statements:
        /// 
        /// list.Clear();
        /// list.TrimExcess();
        /// </summary>
        void TrimExcess();
    }
}
