// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// An interface that represents a dictionary of unordered property or metadata name/value pairs with copy-on-write semantics.
    /// </summary>
    /// <remarks>
    /// The value that this adds over IDictionary&lt;string, T&gt; is:
    ///     - supports copy on write
    ///     - enforces that key = T.Name
    ///     - default enumerator is over values
    ///     - (marginal) enforces the correct key comparer
    ///
    /// Really a Dictionary&lt;string, T&gt; where the key (the name) is obtained from IKeyed.Key.
    /// Is not observable, so if clients wish to observe modifications they must mediate them themselves and
    /// either not expose this collection or expose it through a readonly wrapper.
    ///
    /// This collection is safe for concurrent readers and a single writer.
    /// </remarks>
    /// <typeparam name="T">Property or Metadata class type to store</typeparam>
    internal interface ICopyOnWritePropertyDictionary<T> : IEnumerable<T>, IEquatable<ICopyOnWritePropertyDictionary<T>>, IDictionary<string, T>
        where T : class, IKeyed, IValued, IEquatable<T>, IImmutable
    {
        /// <summary>
        /// Returns true if a property with the specified name is present in the collection, otherwise false.
        /// </summary>
        bool Contains(string name);

        /// <summary>
        /// Add the specified property to the collection.
        /// Overwrites any property with the same name already in the collection.
        /// To remove a property, use Remove(...) instead.
        /// </summary>
        void Set(T projectProperty);

        /// <summary>
        /// Adds the specified properties to this dictionary.
        /// </summary>
        /// <param name="other">An enumerator over the properties to add.</param>
        void ImportProperties(IEnumerable<T> other);

        /// <summary>
        /// Clone. As we're copy on write, this should be cheap.
        /// </summary>
        ICopyOnWritePropertyDictionary<T> DeepClone();
    }
}
