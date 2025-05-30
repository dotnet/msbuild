// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

// Added to the System.Linq extension method as these extensions augment those
// provided by Linq. The immutable collections library includes ImmutableArrayExtensions
// which is also in this namespace.

#nullable disable

namespace System.Linq
{
    internal static class ImmutableCollectionsExtensions
    {
        /// <summary>
        /// Gets a value indicating whether any elements are in this collection
        /// that match a given condition.
        /// </summary>
        /// <remarks>
        /// This extension method accepts an argument which is then passed, on the stack, to the predicate.
        /// This allows using a static lambda, which can avoid a per-call allocation of a closure object.
        /// </remarks>
        /// <typeparam name="TElement">The type of element contained by the collection.</typeparam>
        /// <typeparam name="TArg">The type of argument passed to <paramref name="predicate"/>.</typeparam>
        /// <param name="immutableArray">The array to check.</param>
        /// <param name="predicate">The predicate.</param>
        /// <param name="arg">The argument to pass to <paramref name="predicate"/>.</param>
        public static bool Any<TElement, TArg>(this ImmutableArray<TElement> immutableArray, Func<TElement, TArg, bool> predicate, TArg arg)
        {
            foreach (TElement element in immutableArray)
            {
                if (predicate(element, arg))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
