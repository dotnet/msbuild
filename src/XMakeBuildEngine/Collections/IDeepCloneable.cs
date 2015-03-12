// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>An interface for objects supporting deep clone semantics.</summary>
//-----------------------------------------------------------------------

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// An interface representing an item which can clone itself.
    /// </summary>
    /// <typeparam name="T">The type returned by the clone operation.</typeparam>
    internal interface IDeepCloneable<T>
    {
        /// <summary>
        /// Creates a clone of the item where no data references are shared.  Changes made to the clone
        /// do not affect the original item.
        /// </summary>
        /// <returns>The cloned item.</returns>
        T DeepClone();
    }
}
