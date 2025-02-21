// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// A set of extension methods for working with immutable dictionaries.
    /// </summary>
    internal static class IEnumerableExtensions
    {
        /// <summary>
        /// Avoids allocating an enumerator when enumerating an <see cref="IEnumerable{T}"/> in many cases.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <see langword="foreach"/> statement enumerates types that implement <see cref="IEnumerable{T}"/>,
        /// but will also enumerate any type that has the required methods. Several collection types take advantage of this
        /// to avoid allocating an enumerator on the heap when used with <see langword="foreach"/> by returning a
        /// <see langword="struct"/> enumerator. This is in contrast to the interface-based enumerator
        /// <see cref="IEnumerator{T}"/> which will always be allocated on the heap.
        /// </para>
        /// <para>
        /// This extension method attempts to create a non-allocating struct enumerator to enumerate
        /// <paramref name="collection"/>. It checks the concrete type of the collection and provides a
        /// non-allocating path in several cases.
        /// </para>
        /// <para>
        /// Types that can be enumerated without allocation are:
        /// </para>
        /// <list type="bullet">
        ///     <item><description><see cref="IList{T}"/> (and by extension <see cref="List{T}"/> and other popular implementations)</description></item>
        ///     <item><description><see cref="LinkedList{T}"/></description></item>
        ///     <item><description><see cref="ImmutableHashSet{T}"/></description></item>
        ///     <item><description><see cref="ImmutableList{T}"/></description></item>
        ///     <item><description><see cref="ICollection{T}"/> or <see cref="IReadOnlyCollection{T}"/> having zero count</description></item>
        /// </list>
        /// <para>
        /// If <paramref name="collection"/> is not one of the supported types, the returned enumerator falls back to the
        /// interface-based enumerator, which will heap allocate. Benchmarking shows the overhead in such cases is low enough
        /// to be within the measurement error.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// <![CDATA[IEnumerable<string> collection = ...;
        ///
        /// foreach (string item in collection.GetStructEnumerable())
        /// {
        ///     // ...
        /// }]]>
        /// </code>
        /// </example>
        /// <typeparam name="T">The item type that is enumerated.</typeparam>
        /// <param name="collection">The collections that will be enumerated.</param>
        /// <returns>The enumerator for the collection.</returns>
        public static Enumerable<T> GetStructEnumerable<T>(this IEnumerable<T> collection)
        {
            if (collection is null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            return Enumerable<T>.Create(collection);
        }

        /// <summary>
        /// Provides a struct-based enumerator for use with <see cref="IEnumerable{T}"/>.
        /// Do not use this type directly. Use <see cref="GetStructEnumerable{T}"/> instead.
        /// </summary>
        /// <typeparam name="T">The item type that is enumerated.</typeparam>
        public readonly ref struct Enumerable<T>
        {
            private readonly IEnumerable<T> collection;

            private Enumerable(IEnumerable<T> collection) => this.collection = collection;

            /// <summary>
            /// Constructs an <see cref="Enumerable{T}"/> that can be enumerated.
            /// </summary>
            /// <param name="collection">The collections that will be enumerated.</param>
            /// <returns><see cref="Enumerable{T}"/>.</returns>
            public static Enumerable<T> Create(IEnumerable<T> collection)
            {
                return new Enumerable<T>(collection);
            }

            /// <summary>
            /// Gets the Enumerator.
            /// </summary>
            /// <returns><see cref="Enumerator"/>.</returns>
            public Enumerator GetEnumerator() => new(this.collection);

            /// <summary>
            /// A struct-based enumerator for use with <see cref="IEnumerable{T}"/>.
            /// Do not use this type directly. Use <see cref="GetStructEnumerable{T}"/> instead.
            /// </summary>
            public struct Enumerator : IDisposable
            {
                private readonly Type enumeratorType;
                private readonly IEnumerator<T>? fallbackEnumerator;
                private readonly IList<T>? iList;
                private int listIndex;

                private LinkedList<T>.Enumerator concreteLinkedListEnumerator;
                private ImmutableHashSet<T>.Enumerator concreteImmutableHashSetEnumerator;
                private ImmutableList<T>.Enumerator concreteImmutableListEnumerator;
                private HashSet<T>.Enumerator concreteHashSetEnumerator;

                /// <summary>
                /// Initializes a new instance of the <see cref="Enumerator"/> struct.
                /// </summary>
                /// <param name="collection">The collection that will be enumerated.</param>
                internal Enumerator(IEnumerable<T> collection)
                {
                    this.concreteLinkedListEnumerator = default;
                    this.concreteImmutableHashSetEnumerator = default;
                    this.concreteImmutableListEnumerator = default;
                    this.concreteHashSetEnumerator = default;
                    this.fallbackEnumerator = null;
                    this.iList = null;
                    this.listIndex = -1;

                    switch (collection)
                    {
                        case ICollection<T> sizedCollection when sizedCollection.Count == 0:
                        case IReadOnlyCollection<T> readOnlyCollection when readOnlyCollection.Count == 0:
                            // The collection is empty, just return false from MoveNext.
                            this.enumeratorType = Type.Empty;
                            break;
                        case LinkedList<T> concreteLinkedList:
                            this.enumeratorType = Type.LinkedList;
                            this.concreteLinkedListEnumerator = concreteLinkedList.GetEnumerator();
                            break;
                        case ImmutableHashSet<T> concreteImmutableHashSet:
                            this.enumeratorType = Type.ImmutableHashSet;
                            this.concreteImmutableHashSetEnumerator = concreteImmutableHashSet.GetEnumerator();
                            break;
                        case ImmutableList<T> concreteImmutableList:
                            this.enumeratorType = Type.ImmutableList;
                            this.concreteImmutableListEnumerator = concreteImmutableList.GetEnumerator();
                            break;
                        case HashSet<T> concreteHashSet:
                            this.enumeratorType = Type.HashSet;
                            this.concreteHashSetEnumerator = concreteHashSet.GetEnumerator();
                            break;
                        case IList<T> list:
                            this.enumeratorType = Type.IList;
                            this.iList = list;
                            break;
                        default:
                            this.enumeratorType = Type.Fallback;
                            this.fallbackEnumerator = collection.GetEnumerator();
                            break;
                    }
                }

                private enum Type : byte
                {
                    Empty,
                    IList,
                    LinkedList,
                    ImmutableHashSet,
                    ImmutableList,
                    HashSet,
                    Fallback,
                }

                /// <summary>
                /// Gets the element in the <see cref="IEnumerable{T}"/> at the current position of the enumerator.
                /// </summary>
                public T Current =>
                    this.enumeratorType switch
                    {
                        Type.IList => (uint)this.listIndex < this.iList!.Count ? this.iList![this.listIndex] : default!,
                        Type.LinkedList => this.concreteLinkedListEnumerator.Current,
                        Type.ImmutableHashSet => this.concreteImmutableHashSetEnumerator.Current,
                        Type.ImmutableList => this.concreteImmutableListEnumerator.Current,
                        Type.HashSet => this.concreteHashSetEnumerator.Current,
                        Type.Fallback => this.fallbackEnumerator!.Current,
                        _ => default!,
                    };

                /// <summary>
                /// Advances the enumerator to the next element of the <see cref="IEnumerable{T}"/>.
                /// </summary>
                /// <returns><see langword="true"/> if the enumerator was successfully advanced to the next element; <see langword="false"/> if
                /// the enumerator has passed the end of the <see cref="IEnumerable{T}"/>.</returns>
                public bool MoveNext() =>
                    this.enumeratorType switch
                    {
                        Type.IList => ++this.listIndex < this.iList!.Count,
                        Type.LinkedList => this.concreteLinkedListEnumerator.MoveNext(),
                        Type.ImmutableHashSet => this.concreteImmutableHashSetEnumerator.MoveNext(),
                        Type.ImmutableList => this.concreteImmutableListEnumerator.MoveNext(),
                        Type.HashSet => this.concreteHashSetEnumerator.MoveNext(),
                        Type.Fallback => this.fallbackEnumerator!.MoveNext(),
                        _ => false,
                    };

                /// <summary>
                /// Disposes the underlying enumerator.
                /// </summary>
                public void Dispose()
                {
                    switch (this.enumeratorType)
                    {
                        case Type.LinkedList:
                            this.concreteLinkedListEnumerator.Dispose();
                            break;

                        case Type.ImmutableHashSet:
                            this.concreteImmutableHashSetEnumerator.Dispose();
                            break;

                        case Type.ImmutableList:
                            this.concreteImmutableListEnumerator.Dispose();
                            break;

                        case Type.HashSet:
                            this.concreteHashSetEnumerator.Dispose();
                            break;

                        case Type.Fallback:
                            this.fallbackEnumerator!.Dispose();
                            break;
                    }
                }
            }
        }
    }
}
