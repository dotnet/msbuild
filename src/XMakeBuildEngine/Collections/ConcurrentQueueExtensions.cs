// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Extension methods for the ConcurrentQueue.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// The extensions class for ConcurrentQueue&lt;T&gt;
    /// </summary>
    internal static class ConcurrentQueueExtensions
    {
        /// <summary>
        /// The dequeue method.
        /// </summary>
        /// <typeparam name="T">The type contained within the queue</typeparam>
        static public T Dequeue<T>(this ConcurrentQueue<T> stack) where T : class
        {
            T result = null;
            ErrorUtilities.VerifyThrow(stack.TryDequeue(out result), "Unable to dequeue from queue");
            return result;
        }
    }
}