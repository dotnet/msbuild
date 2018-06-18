// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Strongly typed weak reference</summary>
//-----------------------------------------------------------------------

using System;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// Strongly typed weak reference
    /// </summary>
    /// <typeparam name="T">Type of the target of the weak reference</typeparam>
    internal class WeakReference<T>
        where T : class
    {
        /// <summary>
        /// Cache the hashcode so that it is still available even if the target has been 
        /// collected. This allows this object to be still found in a table so it can be removed.
        /// </summary>
        private readonly int _hashcode;

        /// <summary>
        /// Backing weak reference
        /// </summary>
        private readonly WeakReference _weakReference;

        /// <summary>
        /// Constructor.
        /// Target may not be null.
        /// </summary>
        internal WeakReference(T target)
        {
            ErrorUtilities.VerifyThrowInternalNull(target, nameof(target));

            _weakReference = new WeakReference(target);
            _hashcode = target.GetHashCode();
        }

        /// <summary>
        /// Target wrapped by this weak reference.
        /// If it returns null, its value may have been collected, or it may actually "wrap" null.
        /// To distinguish these cases, compare the WeakReference object itself to WeakReference.Null.
        /// </summary>
        internal T Target => (T)_weakReference.Target;

        /// <summary>
        /// Returns the hashcode of the wrapped target.
        /// </summary>
        public override int GetHashCode()
        {
            return _hashcode;
        }
    }
}
