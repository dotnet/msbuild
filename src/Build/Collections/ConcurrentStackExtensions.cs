// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Extension methods for the ConcurrentStack.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// The extensions class for ConcurrentStack&lt;T&gt;
    /// </summary>
    internal static class ConcurrentStackExtensions
    {
        /// <summary>
        /// The peek method.
        /// </summary>
        /// <typeparam name="T">The type contained within the stack.</typeparam>
        static public T Peek<T>(this ConcurrentStack<T> stack) where T : class
        {
            T result = null;
            ErrorUtilities.VerifyThrow(stack.TryPeek(out result), "Unable to peek from stack");
            return result;
        }

        /// <summary>
        /// The pop method.
        /// </summary>
        /// <typeparam name="T">The type contained within the stack.</typeparam>
        static public T Pop<T>(this ConcurrentStack<T> stack) where T : class
        {
            T result = null;
            ErrorUtilities.VerifyThrow(stack.TryPop(out result), "Unable to pop from stack");
            return result;
        }
    }
}
